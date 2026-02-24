using MemoryPack;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using ZstdSharp;

namespace Nisp.Core.Components
{
    /// <summary>
    /// Represents a client for the NISP protocol.
    /// </summary>
    public sealed class NispClient : IAsyncDisposable
    {
        private TcpClient? _client;
        private Stream? _stream;
        private Compressor? _compressor;
        private readonly ILogger<NispClient>? _logger;    
        private readonly SslOptions? _encryptionOptions;

        /// <summary>
        /// Initializes a new instance of the <see cref="NispClient"/> class.
        /// </summary>
        /// <param name="host">Target hostname or IP address.</param>
        /// <param name="port">Target port number.</param>
        /// <param name="compressionLevel">Level of ZStandard compression for messages.</param>
        /// <param name="encryptionOptions">SSL/TLS configuration options.</param>
        /// <param name="loggerFactory">Optional logger factory instance.</param>
        public NispClient(string host, int port, int? compressionLevel = null, SslOptions? encryptionOptions = null, ILoggerFactory? loggerFactory = null)
        {
            TargetHost = host;
            TargetPort = port;

            if (compressionLevel != null)
                _compressor = new Compressor((int)compressionLevel);

            _encryptionOptions = encryptionOptions;
            _logger = loggerFactory?.CreateLogger<NispClient>();
        }

        /// <summary>
        /// Gets the target hostname or IP address.
        /// </summary>
        public string TargetHost { get; }

        /// <summary>
        /// Gets the target port number.
        /// </summary>
        public int TargetPort { get; }

        /// <summary>
        /// Gets a value indicating whether the client is connected.
        /// </summary>
        public bool IsConnected => _client?.Connected == true && _stream != null;

        /// <summary>
        /// Establishes a connection to the remote endpoint.
        /// </summary>
        /// <param name="delay">Delay between retry attempts in milliseconds.</param>
        /// <param name="sendTimeout">Send operation timeout in milliseconds.</param>
        /// <param name="maxAttempts">Maximum number of connection attempts.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns><c>true</c> if connection succeeded, <c>false</c> otherwise.</returns>
        /// <exception cref="InvalidOperationException">Thrown when already connected.</exception>
        public async Task<bool> ConnectAsync(int delay = 10000, int sendTimeout = 30000, int maxAttempts = 5, CancellationToken cancellationToken = default)
        {
            if (IsConnected)
            {
                _logger?.LogError("The client is already connected to {Host}:{Port}", TargetHost, TargetPort);
                throw new InvalidOperationException($"The client is already connected to {TargetHost}:{TargetPort}");
            }

            _logger?.LogInformation("The client has started connecting to {Host}:{Port}...", TargetHost, TargetPort);

            bool successfullyConnected = false;
            int attempt = 0;

            while (!successfullyConnected && !cancellationToken.IsCancellationRequested && attempt < maxAttempts)
            {
                try
                {
                    var addresses = await Dns.GetHostAddressesAsync(TargetHost, cancellationToken).ConfigureAwait(false);
                    var endpoint = new IPEndPoint(addresses[0], TargetPort);

                    _client = new TcpClient
                    {
                        SendTimeout = sendTimeout
                    };

                    _client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.NoDelay, true);
                    _client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                    _client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

                    await _client.ConnectAsync(endpoint, cancellationToken).ConfigureAwait(false);
                    _stream = _client.GetStream();

                    if (_encryptionOptions != null)
                    {
                        var sslStream = new SslStream(
                            _stream,
                            leaveInnerStreamOpen: false
                        );

                        var options = new SslClientAuthenticationOptions
                        {
                            TargetHost = TargetHost,
                            CertificateRevocationCheckMode = _encryptionOptions.CheckCertificateRevocation ? X509RevocationMode.Online : X509RevocationMode.NoCheck,
                            EnabledSslProtocols = SslProtocols.None,
                            ClientCertificates = [_encryptionOptions.Certificate],
                            RemoteCertificateValidationCallback = (s, cert, ch, errors) =>
                            {
                                if (_encryptionOptions.RemoteCertificateValidationCallback == null)
                                    return ValidateServerCertificate(s, cert, ch, errors);

                                return _encryptionOptions.RemoteCertificateValidationCallback(s, cert, ch, errors);
                            }
                        };

                        await sslStream.AuthenticateAsClientAsync(options, cancellationToken).ConfigureAwait(false);
                        _stream = sslStream;
                    }

                    successfullyConnected = true;
                }
                catch (OperationCanceledException)
                {
                    _logger?.LogInformation("The client has stopped connecting to {Host}:{Port}", TargetHost, TargetPort);
                    return successfullyConnected;
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to connect the client to {Host}:{Port}, next attempt after {Delay} ms", TargetHost, TargetPort, delay);

                    attempt++;

                    if (attempt == maxAttempts)
                        continue;

                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }
            }

            if (successfullyConnected)
                _logger?.LogInformation("The client has successfully connected to {Host}:{Port}", TargetHost, TargetPort);
            else
                _logger?.LogError("The client failed to connect to {Host}:{Port}", TargetHost, TargetPort);

            return successfullyConnected;
        }

        /// <summary>
        /// Sends a message to the connected server.
        /// </summary>
        /// <typeparam name="TMessage">Type of the message.</typeparam>
        /// <param name="message">Message to send.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <exception cref="InvalidOperationException">Thrown when not connected.</exception>
        /// <exception cref="ArgumentNullException">Thrown when message is null.</exception>
        public async Task SendAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default)
        {
            if (!IsConnected)
            {
                _logger?.LogError("The client is not connected to {Host}:{Port}", TargetHost, TargetPort);
                throw new InvalidOperationException($"The client is not connected to {TargetHost}:{TargetPort}");
            }

            ArgumentNullException.ThrowIfNull(message);

            try
            {
                byte[] payload = MemoryPackSerializer.Serialize(message);
                
                if (_compressor != null)
                    payload = _compressor.Wrap(payload).ToArray();

                byte[] header = BitConverter.GetBytes(payload.Length);

                await _stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
                await _stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);

                _logger?.LogInformation("Sent message of type <{Type}> to {Host}:{Port}", typeof(TMessage).Name, TargetHost, TargetPort);
            }
            catch (OperationCanceledException)
            {
                _logger?.LogError("The client stopped to send the message to {Host}:{Port}", TargetHost, TargetPort);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "The client failed to send the message to {Host}:{Port}", TargetHost, TargetPort);
            }
        }

        /// <summary>
        /// Disconnects from the server.
        /// </summary>
        public async ValueTask StopAsync()
        {
            if (!IsConnected)
            {
                _logger?.LogWarning("Couldn't stop the client because it's not connected to {Host}:{Port}", TargetHost, TargetPort);
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
                _client.Dispose();
                _client = null;
            }

            if (_compressor != null)
            {
                _compressor.Dispose();
                _compressor = null;
            }

            _logger?.LogInformation("The client has successfully disconnected from {Host}:{Port}", TargetHost, TargetPort);
        }

        /// <summary>
        /// Disposes the client resources asynchronously.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            await StopAsync().ConfigureAwait(false);
            GC.SuppressFinalize(this);
        }

        private bool ValidateServerCertificate(object sender, X509Certificate? cert, X509Chain? chain, SslPolicyErrors errors)
        {
            if (errors == SslPolicyErrors.None)
                return true;

            _logger?.LogError("Server certificate validation failed: {Error}", errors);
            return false;
        }
    }
}