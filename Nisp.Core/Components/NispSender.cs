using EasyCompressor;
using MemoryPack;
using Microsoft.Extensions.Logging;
using Nisp.Core.Entities;
using System.Buffers.Binary;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace Nisp.Core.Components
{
    /// <summary>
    /// Represents a sender component for the NISP protocol responsible for establishing connections and sending messages to remote endpoints.
    /// </summary>
    public sealed class NispSender : NispBase, IAsyncDisposable
    {
        private TcpClient? _client;
        private Stream? _stream;

        private readonly ILogger<NispSender>? _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="NispSender"/> class.
        /// </summary>
        /// <param name="address">The target IP address to connect to.</param>
        /// <param name="port">The target port number to connect to.</param>
        /// <param name="compressor">Optional compression provider for message payload compression.</param>
        /// <param name="encryptionOptions">Optional SSL/TLS configuration for secure connections.</param>
        /// <param name="messageTypes">Optional list of message type registrations with their unique identifiers.</param>
        /// <param name="loggerFactory">Optional logger factory for creating logging instances.</param>
        public NispSender(IPAddress address, int port, ICompressor? compressor = null, SslOptions? encryptionOptions = null, List<(ushort Id, Type Type)>? messageTypes = null, ILoggerFactory? loggerFactory = null)
            : base(address, port, compressor, encryptionOptions, messageTypes)
        {
            _logger = loggerFactory?.CreateLogger<NispSender>();
        }

        /// <summary>
        /// Gets a value indicating whether the sender is currently connected to the remote endpoint.
        /// </summary>
        /// <value><c>true</c> if the TCP client is connected and the network stream is available; otherwise, <c>false</c>.</value>
        public override bool IsConnected => _client?.Connected == true && _stream != null;

        /// <summary>
        /// Establishes a connection to the configured remote endpoint.
        /// </summary>
        /// <param name="sendTimeout">Send operation timeout in milliseconds. Default is 30000.</param>
        /// <param name="retryDelay">Delay between connection retry attempts in milliseconds. Default is 10000.</param>
        /// <param name="retryAttempts">Maximum number of connection attempts. Default is 5.</param>
        /// <param name="cancellationToken">Cancellation token for aborting the connection attempt.</param>
        /// <returns><c>true</c> if the connection was successfully established; otherwise, <c>false</c>.</returns>
        /// <exception cref="InvalidOperationException">Thrown when attempting to connect while already connected.</exception>
        public async Task<bool> ConnectAsync(int sendTimeout = 30000, int retryDelay = 10000, int retryAttempts = 5, CancellationToken cancellationToken = default)
        {
            if (IsConnected)
            {
                _logger?.LogError("The client is already connected to {Host}:{Port}", IPAddress, Port);
                throw new InvalidOperationException($"The client is already connected to {IPAddress}:{Port}");
            }

            _logger?.LogInformation("The client has started connecting to {Host}:{Port}...", IPAddress, Port);

            bool successfullyConnected = false;
            int attempt = 0;

            while (!successfullyConnected && !cancellationToken.IsCancellationRequested && attempt < retryAttempts)
            {
                try
                {
                    _client = new TcpClient
                    {
                        SendTimeout = sendTimeout
                    };

                    _client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.NoDelay, true);
                    _client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);

                    var endpoint = new IPEndPoint(IPAddress, Port);
                    await _client.ConnectAsync(endpoint, cancellationToken).ConfigureAwait(false);
                    _stream = _client.GetStream();

                    if (SslOptions != null)
                    {
                        var sslStream = new SslStream(
                            _stream!,
                            leaveInnerStreamOpen: false
                        );

                        var options = new SslClientAuthenticationOptions
                        {
                            TargetHost = IPAddress.ToString(),
                            CertificateRevocationCheckMode = SslOptions.CheckCertificateRevocation ? X509RevocationMode.Online : X509RevocationMode.NoCheck,
                            EnabledSslProtocols = SslProtocols.None,
                            ClientCertificates = [SslOptions.Certificate],
                            RemoteCertificateValidationCallback = (s, cert, ch, errors) =>
                            {
                                if (SslOptions.RemoteCertificateValidationCallback == null)
                                    return ValidateCertificate(s, cert, ch, errors);

                                return SslOptions.RemoteCertificateValidationCallback(s, cert, ch, errors);
                            }
                        };

                        await sslStream.AuthenticateAsClientAsync(options, cancellationToken).ConfigureAwait(false);
                        _stream = sslStream;
                    }

                    successfullyConnected = true;
                }
                catch (OperationCanceledException)
                {
                    _logger?.LogInformation("The client has stopped connecting to {Host}:{Port}", IPAddress, Port);
                    return successfullyConnected;
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to connect the client to {Host}:{Port}, next attempt after {Delay} ms", IPAddress, Port, retryDelay);

                    _stream = null;
                    _client = null;

                    attempt++;

                    if (attempt == retryAttempts)
                        continue;

                    await Task.Delay(retryDelay, cancellationToken).ConfigureAwait(false);
                }
            }

            if (successfullyConnected)
                _logger?.LogInformation("The client has successfully connected to {Host}:{Port}", IPAddress, Port);
            else
                _logger?.LogError("The client failed to connect to {Host}:{Port}", IPAddress, Port);

            return successfullyConnected;
        }

        /// <summary>
        /// Serializes and sends a message to the connected remote endpoint.
        /// </summary>
        /// <typeparam name="TMessage">The type of message to send. Must be registered with a type identifier.</typeparam>
        /// <param name="message">The message instance to send.</param>
        /// <param name="cancellationToken">Cancellation token for aborting the send operation.</param>
        /// <exception cref="InvalidOperationException">Thrown when the sender is not connected.</exception>
        /// <exception cref="ArgumentNullException">Thrown when the message is null.</exception>
        public async Task SendAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default)
        {
            if (!IsConnected)
            {
                _logger?.LogError("The client is not connected to {Host}:{Port}", IPAddress, Port);
                throw new InvalidOperationException($"The client is not connected to {IPAddress}:{Port}");
            }

            ArgumentNullException.ThrowIfNull(message);

            try
            {
                byte[] serialized = MemoryPackSerializer.Serialize(message);

                bool isCompressed = Compressor != null;
                var payload = isCompressed ? Compressor!.Compress(serialized) : serialized;

                var packet = new byte[1 + sizeof(ushort) + sizeof(int) + payload.Length];
                packet[0] = isCompressed ? (byte)0x01 : (byte)0x00;

                ushort typeId = MessageTypes.First(t => t.Type == typeof(TMessage)).Id;

                BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(1, 2), typeId);
                BinaryPrimitives.WriteInt32LittleEndian(packet.AsSpan(3, 4), payload.Length);
                Array.Copy(payload, 0, packet, 1 + sizeof(ushort) + sizeof(int), payload.Length);

                await _stream!.WriteAsync(packet, cancellationToken).ConfigureAwait(false);

                _logger?.LogInformation("Sent message of type <{Type}> to {Host}:{Port}", typeof(TMessage).Name, IPAddress, Port);
            }
            catch (OperationCanceledException)
            {
                _logger?.LogError("The client stopped to send the message to {Host}:{Port}", IPAddress, Port);
                throw;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "The client failed to send the message to {Host}:{Port}", IPAddress, Port);
                throw;
            }
        }

        /// <summary>
        /// Gracefully stops the sender, closing the network connection and releasing resources.
        /// </summary>
        /// <returns>A value task representing the asynchronous stop operation.</returns>
        public async ValueTask StopAsync()
        {
            if (_stream != null)
            {
                _stream.Close();
                await _stream.DisposeAsync().ConfigureAwait(false);
                _stream = null;
            }

            if (_client != null)
            {
                _client.Close();
                _client.Dispose();
                _client = null;
            }

            _logger?.LogInformation("The client has successfully disconnected from {Host}:{Port}", IPAddress, Port);
        }

        /// <summary>
        /// Disposes the sender asynchronously, releasing all managed and unmanaged resources.
        /// </summary>
        /// <returns>A value task representing the asynchronous dispose operation.</returns>
        public async ValueTask DisposeAsync()
        {
            await StopAsync().ConfigureAwait(false);
            GC.SuppressFinalize(this);
        }

        protected override bool ValidateCertificate(object sender, X509Certificate? cert, X509Chain? chain, SslPolicyErrors errors)
        {
            if (errors == SslPolicyErrors.None)
                return true;

            _logger?.LogError("Server certificate validation failed: {Error}", errors);
            return false;
        }
    }
}