using EasyCompressor;
using MemoryPack;
using Microsoft.Extensions.Logging;
using Nisp.Core.Entities;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Channels;

namespace Nisp.Core.Components
{
    /// <summary>
    /// Represents a receiver component for the NISP protocol responsible for listening for incoming connections and routing received messages to type-specific channels.
    /// </summary>
    public sealed class NispReceiver : NispBase, IAsyncDisposable
    {
        private TcpListener? _listener;
        private TcpClient? _client;
        private Stream? _stream;
        private CancellationTokenSource? _receiveCts;
        private Task? _receiveLoopTask;
        private bool _isReceiving;

        private readonly ILogger<NispReceiver>? _logger;
        private readonly ConcurrentDictionary<Type, Channel<object>> _channels;

        /// <summary>
        /// Initializes a new instance of the <see cref="NispReceiver"/> class.
        /// </summary>
        /// <param name="address">The IP address to listen on.</param>
        /// <param name="port">The port number to listen on.</param>
        /// <param name="compressor">Optional compression provider for message payload decompression.</param>
        /// <param name="encryptionOptions">Optional SSL/TLS configuration for secure connections.</param>
        /// <param name="messageTypes">Optional list of message type registrations with their unique identifiers.</param>
        /// <param name="loggerFactory">Optional logger factory for creating logging instances.</param>
        public NispReceiver(IPAddress address, int port, ICompressor? compressor = null, SslOptions? encryptionOptions = null, List<(ushort Id, Type Type)>? messageTypes = null, ILoggerFactory? loggerFactory = null)
            : base(address, port, compressor, encryptionOptions, messageTypes)
        {
            _logger = loggerFactory?.CreateLogger<NispReceiver>();
            _channels = [];
        }

        /// <summary>
        /// Gets a value indicating whether the receiver is currently connected to a client.
        /// </summary>
        /// <value><c>true</c> if the TCP listener is active, a client is connected, and the network stream is available; otherwise, <c>false</c>.</value>
        public override bool IsConnected => _listener != null && _client?.Connected == true && _stream != null;

        /// <summary>
        /// Gets a value indicating whether the receiver is actively processing incoming messages.
        /// </summary>
        /// <value><c>true</c> if the receive loop is running; otherwise, <c>false</c>.</value>
        public bool IsReceiving => _isReceiving;

        /// <summary>
        /// Starts listening for incoming connections with retry logic.
        /// </summary>
        /// <param name="retryDelay">Delay between connection retry attempts in milliseconds. Default is 10000 (10 seconds).</param>
        /// <param name="retryAttempts">Maximum number of connection attempts. Default is 5.</param>
        /// <param name="cancellationToken">Cancellation token for aborting the listening operation.</param>
        /// <returns><c>true</c> if a connection was successfully established; otherwise, <c>false</c>.</returns>
        /// <exception cref="InvalidOperationException">Thrown when attempting to listen while already connected.</exception>
        public async Task<bool> ListenAsync(int retryDelay = 10000, int retryAttempts = 5, CancellationToken cancellationToken = default)
        {
            if (IsConnected)
            {
                _logger?.LogError("The receiver could not be started because it is already connected to {Host}:{Port}", IPAddress, Port);
                throw new InvalidOperationException($"The receiver could not be started because it is already connected to {IPAddress}:{Port}");
            }

            _logger?.LogInformation("The receiver has started connecting to {Host}:{Port}...", IPAddress, Port);

            bool successfullyConnected = false;
            int attempt = 0;

            while (!successfullyConnected && !cancellationToken.IsCancellationRequested && attempt < retryAttempts)
            {
                try
                {
                    var endpoint = new IPEndPoint(IPAddress, Port);

                    _listener = new TcpListener(endpoint);
                    _listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                    _listener.Start();

                    _client = await _listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
                    _client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.NoDelay, true);
                    _client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);

                    _stream = _client!.GetStream();

                    if (SslOptions != null)
                    {
                        var sslStream = new SslStream(
                            _stream!,
                            leaveInnerStreamOpen: false
                        );

                        var sslOptions = new SslServerAuthenticationOptions
                        {
                            ServerCertificate = SslOptions.Certificate,
                            ClientCertificateRequired = SslOptions.ClientCertificateRequired,
                            EnabledSslProtocols = SslProtocols.None,
                            CertificateRevocationCheckMode = SslOptions.CheckCertificateRevocation ? X509RevocationMode.Online : X509RevocationMode.NoCheck,
                            RemoteCertificateValidationCallback = (s, cert, ch, errors) =>
                            {
                                if (SslOptions.RemoteCertificateValidationCallback == null)
                                    return ValidateCertificate(s, cert, ch, errors);

                                return SslOptions.RemoteCertificateValidationCallback(s, cert, ch, errors);
                            }  
                        };

                        await sslStream.AuthenticateAsServerAsync(sslOptions, cancellationToken).ConfigureAwait(false);
                        _stream = sslStream;
                    }

                    successfullyConnected = true;
                }
                catch (OperationCanceledException)
                {
                    _logger?.LogInformation("The receiver has stopped listening on {Host}:{Port}", IPAddress, Port);
                    return successfullyConnected;
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to connect the receiver to {Host}:{Port}, next attempt after {Delay} ms", IPAddress, Port, retryDelay);

                    _stream = null;
                    _client = null;
                    _listener = null;
                    
                    attempt++;

                    if (attempt == retryAttempts)
                        continue;

                    await Task.Delay(retryDelay, cancellationToken).ConfigureAwait(false);
                }
            }

            if (successfullyConnected)
                _logger?.LogInformation("The receiver has successfully started listening on {Host}:{Port}", IPAddress, Port);
            else
                _logger?.LogError("The receiver failed to start listening on {Host}:{Port}", IPAddress, Port);

            return successfullyConnected;
        }

        /// <summary>
        /// Starts the background receive loop that processes incoming messages.
        /// </summary>
        /// <param name="receiveErrorBehavior"></param>
        /// <param name="cancellationToken">Cancellation token for stopping the receive loop.</param>
        /// <exception cref="InvalidOperationException">Thrown when the receiver is not connected or the receive loop is already running.</exception>
        public void StartReceiving(ReceiveErrorBehavior receiveErrorBehavior = ReceiveErrorBehavior.StopAndThrow, CancellationToken cancellationToken = default)
        {
            if (!IsConnected)
            {
                _logger?.LogError("Cannot start receiving because server is not listening on {Host}:{Port}", IPAddress, Port);
                throw new InvalidOperationException($"Cannot start receiving because server is not listening on {IPAddress}:{Port}");
            }

            if (_isReceiving)
            {
                _logger?.LogWarning("Receive loop is already running on {Host}:{Port}", IPAddress, Port);
                return;
            }

            _receiveCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _isReceiving = true;
            _receiveLoopTask = Task.Run(() => ReceiveLoopAsync(receiveErrorBehavior, _receiveCts.Token), cancellationToken);

            _logger?.LogInformation("Started receiving messages on {Host}:{Port}", IPAddress, Port);
        }

        /// <summary>
        /// Stops the background receive loop gracefully.
        /// </summary>
        /// <returns>A task representing the asynchronous stop operation.</returns>
        public async Task StopReceivingAsync()
        {
            if (!_isReceiving)
                return;

            _logger?.LogInformation("Stopping receive loop on {Host}:{Port}", IPAddress, Port);

            if (_receiveCts != null)
                await _receiveCts.CancelAsync().ConfigureAwait(false);

            if (_receiveLoopTask != null)
            {
                try
                {
                    await _receiveLoopTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException) 
                {
                    _logger?.LogInformation("Stopped receiving messages on {Host}:{Port}", IPAddress, Port);
                }
            }

            _receiveCts?.Dispose();
            _receiveCts = null;
            _receiveLoopTask = null;
            _isReceiving = false;
        }

        /// <summary>
        /// Returns an asynchronous enumerable that yields messages of the specified type.
        /// </summary>
        /// <typeparam name="TMessage">The type of messages to receive.</typeparam>
        /// <param name="cancellationToken">Cancellation token for stopping the enumeration.</param>
        /// <returns>An asynchronous enumerable of messages of type <typeparamref name="TMessage"/>.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the receive loop is not running.</exception>
        public async IAsyncEnumerable<TMessage> ReceiveAsync<TMessage>([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (!_isReceiving)
            {
                _logger?.LogError("Receive loop is not running.");
                throw new InvalidOperationException("Receive loop is not running.");
            }

            var channel = _channels.GetOrAdd(typeof(TMessage), _ => Channel.CreateUnbounded<object>(new UnboundedChannelOptions
            {
                SingleReader = false,
                SingleWriter = true,
                AllowSynchronousContinuations = false
            }));

            await foreach (var message in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                if (message is TMessage typedMessage)
                    yield return typedMessage;
            }
        }

        /// <summary>
        /// Gracefully stops the receiver, closing the network connection and releasing all resources.
        /// </summary>
        /// <returns>A value task representing the asynchronous stop operation.</returns>
        public async ValueTask StopAsync()
        {
            await StopReceivingAsync().ConfigureAwait(false);

            if (_stream != null)
            {
                _stream.Close();
                await _stream.DisposeAsync().ConfigureAwait(false);
                _stream = null;
            }

            if (_client != null)
            {
                _client.Close();
                _client = null;
            }

            if (_listener != null)
            {
                _listener.Stop();
                _listener.Dispose();
                _listener = null;
            }

            foreach (var channel in _channels.Values)
                channel.Writer.TryComplete();

            _channels.Clear();
            _logger?.LogInformation("The server has been successfully disconnected from {Host}:{Port}", IPAddress, Port);
        }

        /// <summary>
        /// Disposes the receiver asynchronously, releasing all managed and unmanaged resources.
        /// </summary>
        /// <returns>A value task representing the asynchronous dispose operation.</returns>
        public async ValueTask DisposeAsync()
        {
            await StopAsync().ConfigureAwait(false);
            GC.SuppressFinalize(this);
        }

        private async Task ReceiveLoopAsync(ReceiveErrorBehavior receiveErrorBehavior, CancellationToken cancellationToken)
        {
            if (!IsConnected)
            {
                _logger?.LogError("Can not to start receiving messages because the receiver is not listening to {Host}:{Port}", IPAddress, Port);
                throw new InvalidOperationException($"Can not to start receiving messages because the receiver is not listening to {IPAddress}:{Port}");
            }

            while (!cancellationToken.IsCancellationRequested && IsConnected)
            {
                try
                {
                    byte[] headerBuffer = new byte[1 + sizeof(ushort) + sizeof(int)];
                    await ReadExactAsync(headerBuffer, 0, headerBuffer.Length, cancellationToken).ConfigureAwait(false);

                    bool isCompressed = headerBuffer[0] == 0x01;
                    int typeId = BinaryPrimitives.ReadUInt16LittleEndian(headerBuffer.AsSpan(1, 2));
                    int payloadLength = BinaryPrimitives.ReadInt32LittleEndian(headerBuffer.AsSpan(3, 4));

                    byte[] payloadBuffer = new byte[payloadLength];
                    await ReadExactAsync(payloadBuffer, 0, payloadLength, cancellationToken).ConfigureAwait(false);

                    byte[] data;
                    if (isCompressed)
                    {
                        try
                        {
                            data = Compressor!.Decompress(payloadBuffer);
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, "Failed to decompress message from {Host}:{Port}", IPAddress, Port);

                            if (receiveErrorBehavior == ReceiveErrorBehavior.Ignore)
                                continue;
                            else if (receiveErrorBehavior == ReceiveErrorBehavior.Stop)
                                break;
                            else
                                throw new FormatException($"Failed to decompress message from {IPAddress}:{Port}", ex);
                        }
                    }
                    else
                    {
                        data = payloadBuffer;
                    }

                    var messageType = MessageTypes.FirstOrDefault(t => t.Id == typeId).Type;
                    var message = MemoryPackSerializer.Deserialize(messageType, data);

                    if (message == null)
                    {
                        _logger?.LogWarning("Deserialization returned null for message of <{Type}> type from {Host}:{Port}", messageType, IPAddress, Port);

                        if (receiveErrorBehavior == ReceiveErrorBehavior.Ignore)
                            continue;
                        else if (receiveErrorBehavior == ReceiveErrorBehavior.Stop)
                            break;
                        else
                            throw new NullReferenceException($"Deserialization returned null for message of <{messageType}> type from {IPAddress}:{Port}");
                    }

                    var channel = _channels.GetOrAdd(messageType, _ => Channel.CreateUnbounded<object>(new UnboundedChannelOptions
                    {
                        SingleReader = false,
                        SingleWriter = true,
                        AllowSynchronousContinuations = false
                    }));

                    await channel.Writer.WriteAsync(message, cancellationToken);
                    _logger?.LogInformation("Received message of type {Type} from {Host}:{Port}", message.GetType().Name, IPAddress, Port);
                }
                catch (EndOfStreamException)
                {
                    _logger?.LogInformation("All messages from sender have been read.");
                    break;
                }
                catch (OperationCanceledException)
                {
                    _logger?.LogInformation("The receiver has stopped receiving messages from {Host}:{Port}", IPAddress, Port);
                    break;
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "An error occurred while trying to receive a message");

                    if (receiveErrorBehavior == ReceiveErrorBehavior.Stop)
                        break;
                    else if (receiveErrorBehavior == ReceiveErrorBehavior.StopAndThrow)
                        throw;
                }
            }

            foreach (var channel in _channels.Values)
                channel.Writer.TryComplete();
            
            _isReceiving = false;
            _logger?.LogInformation("Receive loop ended on {Host}:{Port}", IPAddress, Port);
        }

        private async Task ReadExactAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            int totalRead = 0;

            while (totalRead < count)
            {
                int bytesRead = await _stream!.ReadAsync(buffer.AsMemory(offset + totalRead, count - totalRead), cancellationToken).ConfigureAwait(false);

                if (bytesRead == 0)
                    throw new EndOfStreamException($"Stream ended unexpectedly. Expected {count} bytes, got {totalRead}");

                totalRead += bytesRead;
            }
        }

        protected override bool ValidateCertificate(object sender, X509Certificate? cert, X509Chain? chain, SslPolicyErrors errors)
        {
            if (errors == SslPolicyErrors.None)
                return true;

            _logger?.LogError("Client certificate validation failed: {Error}", errors);
            return false;
        }
    }
}