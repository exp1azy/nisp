using MemoryPack;
using System.Net;
using System.Net.Sockets;

namespace Nisp.Core.Entities
{
    public class NispProducer : NispActor, IAsyncDisposable
    {
        private TcpClient? _client;
        private NetworkStream? _stream;

        public NispProducer(string host, int port)
        {
            TargetHost = host;
            TargetPort = port;
        }

        internal NispProducer(NispEndpoint endpoint) : this(endpoint.Host, endpoint.Port) { }

        public override bool IsConnected => _client?.Connected == true && _stream?.Socket.Connected == true;

        public override async Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            if (IsConnected)
                throw new InvalidOperationException($"Already connected to {TargetHost}:{TargetPort}");

            var addresses = await Dns.GetHostAddressesAsync(TargetHost, cancellationToken).ConfigureAwait(false);
            var endpoint = new IPEndPoint(addresses[0], TargetPort);

            _client = new TcpClient();

            _client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.NoDelay, true);
            _client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
            _client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

            await _client.ConnectAsync(endpoint, cancellationToken).ConfigureAwait(false);
            _stream = _client.GetStream();
        }

        public async Task SendAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default)
        {
            if (!IsConnected)
                throw new InvalidOperationException("Not connected");

            ArgumentNullException.ThrowIfNull(message);

            byte[] payload = MemoryPackSerializer.Serialize(message);
            byte[] header = BitConverter.GetBytes(payload.Length);

            await _stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
            await _stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
        }

        public override async ValueTask DisposeAsync()
        {
            if (_client != null)
            {
                _client.Close();
                _client.Dispose();
                _client = null;
            }

            if (_stream != null)
            {
                _stream.Close();
                await _stream.DisposeAsync().ConfigureAwait(false);
                _stream = null;
            }

            GC.SuppressFinalize(this);
        }
    }
}
