namespace Nisp.Core.Entities
{
    /// <summary>
    /// Represents a bidirectional communication peer that combines both client and listener capabilities
    /// for full-duplex communication between services. Manages connection lifecycle and message exchange.
    /// </summary>
    public class NispPeer : IAsyncDisposable
    {
        private readonly NispClient _client;
        private readonly NispListener _listener;

        /// <summary>
        /// Initializes a new instance of the NispPeer with specified client and listener components.
        /// </summary>
        /// <param name="client">Configured client instance for outgoing connections.</param>
        /// <param name="listener">Configured listener instance for incoming connections.</param>
        /// <exception cref="ArgumentNullException">Thrown if client or listener is null.</exception>
        public NispPeer(NispClient client, NispListener listener)
        {
            _client = client;
            _listener = listener;
        }

        /// <summary>
        /// Indicates whether the peer is currently connected to both client and listener components.
        /// </summary>
        public bool IsConnected => _client.IsConnected && _listener.IsConnected;

        /// <summary>
        /// Establishes a bidirectional connection between the client and listener components.
        /// </summary>
        /// <param name="delay">Delay between connection attempts in milliseconds.</param>
        /// <param name="sendTimeout">Timeout for send operations in milliseconds.</param>
        /// <param name="maxAttempts">Maximum number of connection attempts.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>
        /// Task that represents the asynchronous operation. The task result contains <c>true</c> if both
        /// connections were established successfully, <c>false</c> otherwise.
        /// </returns>
        /// <remarks>
        /// The method attempts to establish both connections in parallel. If either connection fails,
        /// both will be retried according to the specified parameters.
        /// </remarks>
        public async Task<bool> ConnectAsync(int delay = 10000, int sendTimeout = 30000, int maxAttempts = 5, CancellationToken cancellationToken = default)
        {
            var clientTask = _client.ConnectAsync(delay, sendTimeout, maxAttempts, cancellationToken).ConfigureAwait(false);
            var listenerTask = _listener.ListenAsync(delay, maxAttempts, cancellationToken).ConfigureAwait(false);

            bool clientConnected = await clientTask;
            bool listenerConnected = await listenerTask;

            return clientConnected && listenerConnected;
        }

        /// <summary>
        /// Sends a message through the client connection.
        /// </summary>
        /// <typeparam name="TMessage">Type of the message to send.</typeparam>
        /// <param name="message">Message payload to send.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Task representing the asynchronous send operation.</returns>
        public async Task SendAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default)
        {
            await _client.SendAsync(message, cancellationToken);
        }

        /// <summary>
        /// Receives messages from the listener connection as an asynchronous enumerable.
        /// </summary>
        /// <typeparam name="TMessage">Type of messages to receive.</typeparam>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>
        /// Asynchronous enumerable of received messages. The enumeration will complete when
        /// the connection is closed or an error occurs.
        /// </returns>
        public IAsyncEnumerable<TMessage> ReceiveAsync<TMessage>(CancellationToken cancellationToken = default)
        {
            return _listener.ReceiveAsync<TMessage>(cancellationToken);
        }

        /// <summary>
        /// Gracefully stops both client and listener connections
        /// </summary>
        public async ValueTask StopAsync()
        {
            var clientTask = _client.StopAsync().ConfigureAwait(false);
            var listenerTask = _listener.StopAsync().ConfigureAwait(false);

            await clientTask;
            await listenerTask;
        }

        /// <summary>
        /// Disposes of both client and listener resources asynchronously
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            await _client.DisposeAsync();
            await _listener.DisposeAsync();
            GC.SuppressFinalize(this);
        }
    }
}