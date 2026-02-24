using MemoryPack;
using Microsoft.Extensions.Logging;
using System.Buffers.Binary;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using ZstdSharp;

namespace Nisp.Core.Components
{
    /// <summary>
    /// Represents a receiver for the NISP protocol.
    /// </summary>
    public sealed class NispReceiver : IAsyncDisposable
    {
        private TcpListener? _listener;
        private TcpClient? _client;
        private Stream? _stream;
        private Decompressor? _decompressor;
        private readonly byte[] _header = new byte[4];
        private readonly ILogger<NispReceiver>? _logger;
        private readonly SslOptions? _encryptionOptions;
        
        /// <summary>
        /// Initializes a new instance of the <see cref="NispReceiver"/> class.
        /// </summary>
        /// <param name="host">The hostname or IP address to listen on.</param>
        /// <param name="port">The port number to listen on.</param>
        /// <param name="compressionEnabled">Enable ZStandard compression for received messages.</param>
        /// <param name="encryptionOptions">SSL/TLS configuration options.</param>
        /// <param name="loggerFactory">Optional logger factory instance.</param>
        public NispReceiver(string host, int port, bool compressionEnabled = false, SslOptions? encryptionOptions = null, ILoggerFactory? loggerFactory = null)
        {
            TargetHost = host;
            TargetPort = port;

            if (compressionEnabled)
                _decompressor = new Decompressor();

            _encryptionOptions = encryptionOptions;
            _logger = loggerFactory?.CreateLogger<NispReceiver>();
        }

        /// <summary>
        /// The hostname or IP address to listen on.
        /// </summary>
        public string TargetHost { get; }

        /// <summary>
        /// The port number to listen on.
        /// </summary>
        public int TargetPort { get; }

        /// <summary>
        /// Indicates whether the listener is currently connected to a client.
        /// </summary>
        public bool IsConnected => _listener != null && _client?.Connected == true && _stream != null;

        /// <summary>
        /// Starts listening for incoming connections.
        /// </summary>
        /// <param name="delay">Delay between retry attempts in milliseconds.</param>
        /// <param name="maxAttempts">Maximum number of connection attempts.</param>
        /// <param name="cancellationToken">Cancellation token for aborting the operation.</param>
        /// <returns><c>true</c> if listening started successfully, <c>false</c> otherwise.</returns>
        /// <exception cref="InvalidOperationException">Thrown when already connected.</exception>
        public async Task<bool> ListenAsync(int delay = 10000, int maxAttempts = 5, CancellationToken cancellationToken = default)
        {
            if (IsConnected)
            {
                _logger?.LogError("The listener could not be started because he is already connected to {Host}:{Port}", TargetHost, TargetPort);
                throw new InvalidOperationException($"The listener could not be started because he is already connected to {TargetHost}:{TargetPort}");
            }

            _logger?.LogInformation("The listener has started connecting to {Host}:{Port}...", TargetHost, TargetPort);

            bool successfullyConnected = false;
            int attempt = 0;

            while (!successfullyConnected && !cancellationToken.IsCancellationRequested && attempt < maxAttempts)
            {
                try
                {
                    var addresses = await Dns.GetHostAddressesAsync(TargetHost, cancellationToken).ConfigureAwait(false);
                    var endpoint = new IPEndPoint(addresses[0], TargetPort);

                    _listener = new TcpListener(endpoint);

                    _listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.NoDelay, true);
                    _listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                    _listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

                    _listener.Start();
                    _client = await _listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
                    _stream = _client.GetStream();

                    if (_encryptionOptions != null)
                    {
                        var sslStream = new SslStream(
                            _stream,
                            leaveInnerStreamOpen: false
                        );

                        var sslOptions = new SslServerAuthenticationOptions
                        {
                            ServerCertificate = _encryptionOptions.Certificate,
                            ClientCertificateRequired = _encryptionOptions.ClientCertificateRequired,
                            EnabledSslProtocols = SslProtocols.None,
                            CertificateRevocationCheckMode = _encryptionOptions.CheckCertificateRevocation ? X509RevocationMode.Online : X509RevocationMode.NoCheck,
                            RemoteCertificateValidationCallback = (s, cert, ch, errors) =>
                            {
                                if (_encryptionOptions.RemoteCertificateValidationCallback == null)
                                    return ValidateClientCertificate(s, cert, ch, errors);

                                return _encryptionOptions.RemoteCertificateValidationCallback(s, cert, ch, errors);
                            }  
                        };

                        await sslStream.AuthenticateAsServerAsync(sslOptions, cancellationToken).ConfigureAwait(false);
                        _stream = sslStream;
                    }

                    successfullyConnected = true;
                }
                catch (OperationCanceledException)
                {
                    _logger?.LogInformation("The server has stopped listening on {Host}:{Port}", TargetHost, TargetPort);
                    return successfullyConnected;
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to connect the listener to {Host}:{Port}, next attempt after {Delay} ms", TargetHost, TargetPort, delay);

                    attempt++;

                    if (attempt == maxAttempts)
                        continue;

                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }
            }

            if (successfullyConnected)
                _logger?.LogInformation("The server has successfully started listening on {Host}:{Port}", TargetHost, TargetPort);
            else
                _logger?.LogError("The server failed to start listening on {Host}:{Port}", TargetHost, TargetPort);

            return successfullyConnected;
        }

        /// <summary>
        /// Asynchronously receives messages from the connected client.
        /// </summary>
        /// <typeparam name="TMessage">Type of the message to receive.</typeparam>
        /// <param name="cancellationToken">Cancellation token for aborting the operation.</param>
        /// <returns>Async enumerable of received messages.</returns>
        /// <exception cref="InvalidOperationException">Thrown when not connected.</exception>
        public async IAsyncEnumerable<TMessage> ReceiveAsync<TMessage>([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (!IsConnected)
            {
                _logger?.LogError("It was not possible to start receiving messages because the server is not listening to {Host}:{Port}", TargetHost, TargetPort);
                throw new InvalidOperationException($"It was not possible to start receiving messages because the server is not listening to {TargetHost}:{TargetPort}");
            }

            while (!cancellationToken.IsCancellationRequested && IsConnected)
            {
                TMessage? message = default;

                try
                {
                    await _stream.ReadExactlyAsync(_header, cancellationToken).ConfigureAwait(false);
                    int messageSize = BinaryPrimitives.ReadInt32LittleEndian(_header);

                    byte[] payload = new byte[messageSize];
                    await _stream.ReadExactlyAsync(payload, cancellationToken).ConfigureAwait(false);

                    if (_decompressor != null)
                        payload = _decompressor.Unwrap(payload).ToArray();

                    message = MemoryPackSerializer.Deserialize<TMessage>(payload);
                }
                catch (EndOfStreamException)
                {
                    _logger?.LogInformation("All messages from client has been readed.");
                    yield break;
                }
                catch (OperationCanceledException)
                {
                    _logger?.LogInformation("The server has stopped receiving messages from {Host}:{Port}", TargetHost, TargetPort);
                    yield break;
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "An error occurred while trying to receive a message");
                    yield break;
                }

                _logger?.LogInformation("Received message of type {Type} from {Host}:{Port}", typeof(TMessage).Name, TargetHost, TargetPort);
                yield return message;
            }
        }

        /// <summary>
        /// Stops listening for incoming connections.
        /// </summary>
        public async ValueTask StopAsync()
        {
            if (!IsConnected)
            {
                _logger?.LogWarning("Couldn't stop the server because it's not listening on {Host}:{Port}", TargetHost, TargetPort);
                return;
            }

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

            if (_decompressor != null)
            {
                _decompressor.Dispose();
                _decompressor = null;
            }

            _logger?.LogInformation("The server has been successfully disconnected from {Host}:{Port}", TargetHost, TargetPort);
        }

        /// <summary>
        /// Disposes the listener resources asynchronously.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            await StopAsync().ConfigureAwait(false);
            GC.SuppressFinalize(this);
        }

        private bool ValidateClientCertificate(object sender, X509Certificate? cert, X509Chain? chain, SslPolicyErrors errors)
        {
            if (errors == SslPolicyErrors.None)
                return true;

            _logger?.LogError("Client certificate validation failed: {Error}", errors);
            return false;
        }
    }
}