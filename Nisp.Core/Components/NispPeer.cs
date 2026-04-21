using Microsoft.Extensions.Logging;
using Nisp.Core.Entities;
using Nisp.Core.Messages;
using System.Runtime.CompilerServices;

namespace Nisp.Core.Components
{
    /// <summary>
    /// Represents a bidirectional communication peer that combines both sender and receiver capabilities for full-duplex communication between services.
    /// </summary>
    /// <remarks>
    /// The NispPeer class provides a convenient wrapper around <see cref="NispSender"/> and <see cref="NispReceiver"/>, managing the connection lifecycle for both components simultaneously.
    /// </remarks>
    public class NispPeer : IAsyncDisposable
    {
        private Task? _imAliveTask;
        private CancellationTokenSource? _ctsForImAlive;

        private readonly NispSender _sender;
        private readonly NispReceiver _receiver;
        private readonly Guid _id;
        private readonly ImAliveConfig? _imAliveConfig;
        private readonly bool _acknowledge;
        private readonly ILogger? _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="NispPeer"/> class.
        /// </summary>
        /// <param name="client">The configured sender component for outgoing messages.</param>
        /// <param name="receiver">The configured receiver component for incoming messages.</param>
        /// <param name="imAliveConfig">Optional configuration for automatic heartbeat messages.</param>
        /// <param name="loggerFactory">Optional logger factory for creating logging instances.</param>
        public NispPeer(NispSender client, NispReceiver receiver, ImAliveConfig? imAliveConfig = null, bool acknowledge = false, ILoggerFactory? loggerFactory = null)
        {
            _sender = client;
            _receiver = receiver;
            _id = Guid.NewGuid();
            _imAliveConfig = imAliveConfig;
            _acknowledge = acknowledge;
            _logger = loggerFactory?.CreateLogger<NispPeer>();
        }

        /// <summary>
        /// Gets a value indicating whether both sender and receiver components are connected.
        /// </summary>
        /// <value><c>true</c> if both the sender is connected and the receiver is listening; otherwise, <c>false</c>.</value>
        public bool IsConnected => _sender.IsConnected && _receiver.IsConnected;

        /// <summary>
        /// Gets the unique identifier of this peer instance.
        /// </summary>
        /// <value>A <see cref="Guid"/> that uniquely identifies this peer across the network.</value>
        /// <remarks>
        /// This identifier is used in <see cref="ImAliveMessage"/> messages to allow remote peers to distinguish between different instances.
        /// </remarks>
        public Guid Id => _id;

        /// <summary>
        /// Establishes connections for both sender and receiver components simultaneously.
        /// </summary>
        /// <param name="retryDelay">Delay between connection retry attempts in milliseconds. Default is 10000.</param>
        /// <param name="sendTimeout">Send operation timeout for the sender in milliseconds. Default is 30000.</param>
        /// <param name="retryAttempts">Maximum number of connection attempts. Default is 5.</param>
        /// <param name="cancellationToken">Cancellation token for aborting the connection attempts.</param>
        /// <returns><c>true</c> if both components connected successfully; otherwise, <c>false</c>.</returns>
        public async Task<bool> ConnectAsync(int sendTimeout = 30000, int retryDelay = 10000, int retryAttempts = 5, CancellationToken cancellationToken = default)
        {
            var clientTask = _sender.ConnectAsync(sendTimeout, retryDelay, retryAttempts, cancellationToken);
            var receiverTask = _receiver.ListenAsync(retryDelay, retryAttempts, cancellationToken);
            await Task.WhenAll(clientTask, receiverTask).ConfigureAwait(false);

            return clientTask.Result && receiverTask.Result;
        }

        /// <summary>
        /// Starts the background message receiving loop and optionally the ImAlive heartbeat loop.
        /// </summary>
        /// <param name="receiveErrorBehavior">Specifies how errors in the receive loop should be handled.</param>
        /// <param name="cancellationToken">Cancellation token for stopping the receive loops.</param>
        /// <exception cref="InvalidOperationException">Thrown when the receiver is not connected.</exception>
        /// <remarks>
        /// This method must be called after a successful connection.
        /// </remarks>
        public void StartReceiving(ReceiveErrorBehavior receiveErrorBehavior = ReceiveErrorBehavior.StopAndThrow, CancellationToken cancellationToken = default)
        {
            _receiver.StartReceiving(receiveErrorBehavior, cancellationToken);

            if (_imAliveConfig != null && _imAliveConfig.DelayInMilliseconds > 0)
                StartImAliveLoop(cancellationToken);
        }

        /// <summary>
        /// Stops the background message receiving loop and the ImAlive heartbeat loop gracefully.
        /// </summary>
        /// <returns>A task representing the asynchronous stop operation.</returns>
        /// <remarks>
        /// This method cancels both the receive loop and the ImAlive heartbeat loop, waiting for them
        /// to complete before returning. The connections remain open and can be used for sending,
        /// but no new messages will be received until <see cref="StartReceiving"/> is called again.
        /// </remarks>
        public async Task StopReceivingAsync()
        {
            await _receiver.StopReceivingAsync().ConfigureAwait(false);
            await StopImAliveLoopAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Sends a message to the remote peer through the sender component.
        /// </summary>
        /// <typeparam name="TMessage">The type of message to send.</typeparam>
        /// <param name="message">The message instance to send.</param>
        /// <param name="cancellationToken">Cancellation token for aborting the send operation.</param>
        /// <returns>A task representing the asynchronous send operation.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the sender is not connected.</exception>
        /// <exception cref="ArgumentNullException">Thrown when the message is null.</exception>
        public async Task SendAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default)
        {
            await _sender.SendAsync(message, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Returns an asynchronous enumerable that yields messages of the specified type from the receiver.
        /// </summary>
        /// <typeparam name="TMessage">The type of messages to receive.</typeparam>
        /// <param name="cancellationToken">Cancellation token for stopping the enumeration.</param>
        /// <returns>An asynchronous enumerable of messages of type <typeparamref name="TMessage"/>.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the receive loop is not running.</exception>
        public IAsyncEnumerable<TMessage> ReceiveAsync<TMessage>(CancellationToken cancellationToken = default)
        {
            var messages = _receiver.ReceiveAsync<TMessage>(cancellationToken);

            if (_acknowledge)
                return WrapWithAckAsync(messages, cancellationToken);

            return messages;
        }

        /// <summary>
        /// Gracefully closes both sender and receiver connections, stopping all background tasks.
        /// </summary>
        /// <returns>A task representing the asynchronous close operation.</returns>
        public async Task CloseConnectionsAsync()
        {
            await StopImAliveLoopAsync().ConfigureAwait(false);

            var clientTask = _sender.StopAsync().AsTask();
            var receiverTask = _receiver.StopAsync().AsTask();

            await Task.WhenAll(clientTask, receiverTask);
        }

        /// <summary>
        /// Disposes the peer asynchronously, releasing all resources.
        /// </summary>
        /// <returns>A value task representing the asynchronous dispose operation.</returns>
        public async ValueTask DisposeAsync()
        {
            await _sender.DisposeAsync().ConfigureAwait(false);
            await _receiver.DisposeAsync().ConfigureAwait(false);
            GC.SuppressFinalize(this);
        }

        private async IAsyncEnumerable<TMessage> WrapWithAckAsync<TMessage>(IAsyncEnumerable<TMessage> source, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await foreach (var message in source.WithCancellation(cancellationToken))
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await SendAsync(new AckMessage
                        {
                            PeerId = _id,
                            TypeReceived = typeof(TMessage).Name,
                            DateTime = DateTime.UtcNow
                        }, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Failed to send ACK for message type {Type}", typeof(TMessage).Name);
                    }
                }, cancellationToken);

                yield return message;
            }
        }

        private void StartImAliveLoop(CancellationToken cancellationToken)
        {
            _logger?.LogInformation("Sending <ImAliveMessage> is enabled");

            _ctsForImAlive = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _imAliveTask = Task.Run(async () =>
            {
                while (!_ctsForImAlive.IsCancellationRequested && IsConnected)
                {
                    try
                    {
                        var msg = new ImAliveMessage
                        {
                            Id = _id,
                            DateTime = DateTime.UtcNow
                        };

                        await _sender.SendAsync(msg, _ctsForImAlive.Token);
                    }
                    catch (OperationCanceledException) { continue; }
                    catch (Exception) { throw; }

                    await Task.Delay(_imAliveConfig!.DelayInMilliseconds, _ctsForImAlive.Token);
                }
            }, _ctsForImAlive.Token);
        }

        private async Task StopImAliveLoopAsync()
        {
            if (_imAliveTask != null && _ctsForImAlive != null)
            {
                await _ctsForImAlive.CancelAsync().ConfigureAwait(false);

                try
                {
                    await _imAliveTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException) { }

                _ctsForImAlive.Dispose();
                _ctsForImAlive = null;
                _imAliveTask = null;
            }
        }
    }
}