namespace Nisp.Core.Entities
{
    public class NispPeer : IAsyncDisposable
    {
        private readonly NispClient _client;
        private readonly NispListener _listener;

        public NispPeer(NispClient client, NispListener listener)
        {
            _client = client;
            _listener = listener;
        }

        public bool IsConnected => _client.IsConnected && _listener.IsConnected;

        public async Task<bool> ConnectAsync(int delay = 10000, int sendTimeout = 30000, int maxAttempts = 5, CancellationToken cancellationToken = default)
        {
            var clientTask = _client.ConnectAsync(delay, sendTimeout, maxAttempts, cancellationToken).ConfigureAwait(false);
            var listenerTask = _listener.ListenAsync(delay, maxAttempts, cancellationToken).ConfigureAwait(false);

            bool clientConnected = await clientTask;
            bool listenerConnected = await listenerTask;

            return clientConnected && listenerConnected;
        }

        public async Task SendAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default)
        {
            await _client.SendAsync(message, cancellationToken);
        }

        public IAsyncEnumerable<TMessage> ReceiveAsync<TMessage>(CancellationToken cancellationToken = default)
        {
            return _listener.ReceiveAsync<TMessage>(cancellationToken);
        }

        public async ValueTask StopAsync()
        {
            var clientTask = _client.StopAsync().ConfigureAwait(false);
            var listenerTask = _listener.StopAsync().ConfigureAwait(false);

            await clientTask;
            await listenerTask;
        }

        public async ValueTask DisposeAsync()
        {
            await _client.DisposeAsync();
            await _listener.DisposeAsync();
            GC.SuppressFinalize(this);
        }
    }
}