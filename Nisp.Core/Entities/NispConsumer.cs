using MemoryPack;
using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;

namespace Nisp.Core.Entities
{
    public class NispConsumer : NispActor, IAsyncDisposable
    {
        private TcpListener? _listener;
        private TcpClient? _client;
        private NetworkStream? _stream;

        public NispConsumer(string host, int port)
        {
            TargetHost = host;
            TargetPort = port;
        }

        internal NispConsumer(NispEndpoint endpoint) : this(endpoint.Host, endpoint.Port) { }

        public override bool IsConnected => 
            _listener != null && 
            _client?.Connected == true && 
            _stream?.Socket.Connected == true;

        public override async Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            if (IsConnected)
                throw new InvalidOperationException($"Already connected to {TargetHost}:{TargetPort}");

            var addresses = await Dns.GetHostAddressesAsync(TargetHost, cancellationToken).ConfigureAwait(false);
            var endpoint = new IPEndPoint(addresses[0], TargetPort);

            _listener = new TcpListener(endpoint);

            _listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.NoDelay, true);
            _listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
            _listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

            _listener.Start();
            _client = await _listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
            _stream = _client.GetStream();
        }

        public async Task<TMessage?> ReceiveAsync<TMessage>(CancellationToken cancellationToken = default)
        {
            if (!IsConnected)
                throw new InvalidOperationException("Not connected");

            var header = new byte[4];
            await _stream.ReadExactlyAsync(header, cancellationToken).ConfigureAwait(false);
            int messageSize = BinaryPrimitives.ReadInt32LittleEndian(header);

            byte[] payload = new byte[messageSize];
            await _stream.ReadExactlyAsync(payload, cancellationToken).ConfigureAwait(false);

            return MemoryPackSerializer.Deserialize<TMessage>(payload);
        }

        public override async ValueTask DisposeAsync()
        {
            if (_listener != null)
            {
                _listener.Stop();
                _listener.Dispose();
                _listener = null;
            }

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
