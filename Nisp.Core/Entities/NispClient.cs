using K4os.Compression.LZ4;
using MemoryPack;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Sockets;
using ZLogger;

namespace Nisp.Core.Entities
{
    public sealed class NispClient : IAsyncDisposable
    {
        private TcpClient? _client;
        private NetworkStream? _stream;
        private readonly ILogger<NispService>? _logger;
        private readonly bool _compressionEnabled;

        public NispClient(string host, int port, bool compressionEnabled = false, ILogger<NispService>? logger = null)
        {
            TargetHost = host;
            TargetPort = port;

            _compressionEnabled = compressionEnabled;
            _logger = logger;
        }

        public string TargetHost { get; }
        public int TargetPort { get; }
        public bool IsConnected => _client?.Connected == true && _stream?.Socket.Connected == true;

        public async Task<bool> ConnectAsync(int delay = 10000, int sendTimeout = 30000, int maxAttemts = 5, CancellationToken cancellationToken = default)
        {
            if (IsConnected)
            {
                _logger?.ZLogError($"[{DateTime.UtcNow}] The client is already connected to {TargetHost}:{TargetPort}");
                throw new InvalidOperationException($"The client is already connected to {TargetHost}:{TargetPort}");
            }

            _logger?.ZLogInformation($"[{DateTime.UtcNow}] The client has started connecting to {TargetHost}:{TargetPort}...");

            bool successfullyConnected = false;
            int attempt = 0;

            while (!successfullyConnected && !cancellationToken.IsCancellationRequested && attempt < maxAttemts)
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

                    successfullyConnected = true;
                }
                catch (Exception)
                {
                    _logger?.ZLogWarning($"[{DateTime.UtcNow}] Failed to connect the client to {TargetHost}:{TargetPort}, next attempt after {delay} ms");
                    
                    attempt++;
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }
            }

            if (successfullyConnected)
                _logger?.ZLogInformation($"[{DateTime.UtcNow}] The client has successfully connected to {TargetHost}:{TargetPort}");
            else
                _logger?.ZLogError($"[{DateTime.UtcNow}] The client failed to connect to {TargetHost}:{TargetPort}");

            return successfullyConnected;
        }

        public async Task SendAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default)
        {
            if (!IsConnected)
            {
                _logger?.ZLogError($"[{DateTime.UtcNow}] The client is not connected to {TargetHost}:{TargetPort}");
                throw new InvalidOperationException($"The client is not connected to {TargetHost}:{TargetPort}");
            }
              
            ArgumentNullException.ThrowIfNull(message);

            byte[] payload = MemoryPackSerializer.Serialize(message);

            if (_compressionEnabled)
                payload = LZ4Pickler.Pickle(payload);

            byte[] header = BitConverter.GetBytes(payload.Length);

            await _stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
            await _stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);

            _logger?.ZLogInformation($"[{DateTime.UtcNow}] Sent message of type {typeof(TMessage).Name} to {TargetHost}:{TargetPort}");
        }

        public async ValueTask StopAsync()
        {
            if (!IsConnected)
            {
                _logger?.ZLogWarning($"[{DateTime.UtcNow}] Couldn't stop the client because it's not connected to {TargetHost}:{TargetPort}");
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

            _logger?.ZLogInformation($"[{DateTime.UtcNow}] The client has successfully disconnected from {TargetHost}:{TargetPort}");
        }

        public async ValueTask DisposeAsync()
        {
            await StopAsync().ConfigureAwait(false);
            GC.SuppressFinalize(this);
        }
    }
}