using K4os.Compression.LZ4;
using MemoryPack;
using Microsoft.Extensions.Logging;
using System.Buffers.Binary;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using ZLogger;

namespace Nisp.Core.Entities
{
    public sealed class NispListener : IAsyncDisposable
    {
        private TcpListener? _listener;
        private TcpClient? _client;
        private Stream? _stream;
        private readonly byte[] _header = new byte[4];
        private readonly ILogger<NispService>? _logger;
        private readonly SslOptions? _encryptionOptions;
        private readonly bool _compressionEnabled;

        public NispListener(string host, int port, bool compressionEnabled, SslOptions? encryptionOptions = null, ILogger<NispService>? logger = null)
        {
            TargetHost = host;
            TargetPort = port;

            _compressionEnabled = compressionEnabled;
            _encryptionOptions = encryptionOptions;
            _logger = logger;
        }

        public string TargetHost { get; }
        public int TargetPort { get; }
        public bool IsConnected => _listener != null && _client?.Connected == true && _stream != null;

        public async Task<bool> ListenAsync(int delay = 10000, int maxAttempts = 5, CancellationToken cancellationToken = default)
        {
            if (IsConnected)
            {
                _logger?.ZLogError($"[{DateTime.UtcNow}] The listener could not be started because he is already connected to {TargetHost}:{TargetPort}");
                throw new InvalidOperationException($"The listener could not be started because he is already connected to {TargetHost}:{TargetPort}");
            }

            _logger?.ZLogInformation($"[{DateTime.UtcNow}] The listener has started connecting to {TargetHost}:{TargetPort}...");

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
                            EnabledSslProtocols = SslProtocols.Tls13,
                            ClientCertificateRequired = _encryptionOptions.ClientCertificateRequired,
                            CertificateRevocationCheckMode = _encryptionOptions.CheckCertificateRevocation ? X509RevocationMode.Online : X509RevocationMode.NoCheck
                        };

                        await sslStream.AuthenticateAsServerAsync(sslOptions, cancellationToken).ConfigureAwait(false);
                        _stream = sslStream;
                    }

                    successfullyConnected = true;
                }
                catch (Exception)
                {
                    _logger?.ZLogWarning($"[{DateTime.UtcNow}] Failed to connect the listener to {TargetHost}:{TargetPort}, next attempt after {delay} ms");

                    attempt++;
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }
            }

            if (successfullyConnected)
                _logger?.ZLogInformation($"[{DateTime.UtcNow}] The server has successfully started listening on {TargetHost}:{TargetPort}");
            else
                _logger?.ZLogError($"[{DateTime.UtcNow}] The server failed to start listening on {TargetHost}:{TargetPort}");

            return successfullyConnected;
        }

        public async IAsyncEnumerable<TMessage> ReceiveAsync<TMessage>([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (!IsConnected)
            {
                _logger?.ZLogError($"[{DateTime.UtcNow}] It was not possible to start receiving messages because the server is not listening to {TargetHost}:{TargetPort}");
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

                    if (_compressionEnabled)
                        payload = LZ4Pickler.Unpickle(payload);

                    message = MemoryPackSerializer.Deserialize<TMessage>(payload);
                }
                catch (Exception ex)
                {
                    _logger?.ZLogError($"[{DateTime.UtcNow}] {ex.Message}");
                    yield break;
                }
                finally
                {
                    Array.Clear(_header, 0, _header.Length);
                }

                _logger?.ZLogInformation($"[{DateTime.UtcNow}] Received message of type {typeof(TMessage).Name} from {TargetHost}:{TargetPort}");
                yield return message;
            }
        }

        public async ValueTask StopAsync()
        {
            if (!IsConnected)
            {
                _logger?.ZLogWarning($"[{DateTime.UtcNow}] Couldn't stop the server because it's not listening on {TargetHost}:{TargetPort}");
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

            if (_listener != null)
            {
                _listener.Stop();
                _listener.Dispose();
                _listener = null;
            }

            _logger?.ZLogInformation($"[{DateTime.UtcNow}] The server has been successfully disconnected from {TargetHost}:{TargetPort}");
        }

        public async ValueTask DisposeAsync()
        {
            await StopAsync().ConfigureAwait(false);
            GC.SuppressFinalize(this);
        }
    }
}