using Microsoft.Extensions.Logging;
using Nisp.Core.Entities;
using System.Runtime.CompilerServices;
using ZLogger;

namespace Nisp.Core
{
    public class NispService
    {
        private readonly Dictionary<Endpoint, NispProducer> _producers = [];
        private readonly Dictionary<Endpoint, NispConsumer> _consumers = [];
        private ILogger<NispService>? _logger;

        public void EnableLogging(Action<ILoggingBuilder> builder)
        {
            var loggerFactory = LoggerFactory.Create(builder);
            _logger = loggerFactory.CreateLogger<NispService>();
        }

        public async Task CreateCoherenceAsync(CoherenceParams config, CancellationToken cancellationToken = default)
        {
            switch (config.Type)
            {
                case CoherenceType.Client:
                    await StartClientAsync(config.Endpoint.Host, config.Endpoint.Port, config.Delay, config.SendTimeout ?? 30000, cancellationToken).ConfigureAwait(false);
                    break;

                case CoherenceType.Listener:
                    await StartListenerAsync(config.Endpoint.Host, config.Endpoint.Port, config.Delay, cancellationToken).ConfigureAwait(false);
                    break;

                case CoherenceType.ClientListener:
                    await StartClientListenerAsync(config.Endpoint.Host, config.Endpoint.Port, config.Delay, config.SendTimeout ?? 30000, cancellationToken).ConfigureAwait(false);
                    break;

                default:
                    throw new ArgumentException("Invalid coherence type");
            }
        }

        public async Task StartClientAsync(string host, int port, int delay = 1000, int sendTimeout = 30000, CancellationToken cancellationToken = default)
        {
            var endpoint = Endpoint.Parse(host, port);
            var producer = new NispProducer(endpoint);

            if (!_producers.TryAdd(endpoint, producer))
            {
                _logger?.ZLogInformation($"[{DateTime.UtcNow}] The client is already connected to {host}:{port}");
                throw new InvalidOperationException($"The client is already connected to {host}:{port}");
            }

            bool successfullyConnected = false;

            while (!successfullyConnected && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await producer.ConnectAsync(sendTimeout, cancellationToken).ConfigureAwait(false);
                    successfullyConnected = true;
                }
                catch (Exception)
                {
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }
            }

            if (successfullyConnected && _logger is not null)
                _logger.ZLogInformation($"[{DateTime.UtcNow}] The client has successfully connected to {host}:{port}");
            else if (!successfullyConnected && _logger is not null)
                _logger.ZLogInformation($"[{DateTime.UtcNow}] The client failed to connect to {host}:{port}");
        }

        public async Task StartListenerAsync(string host, int port, int delay = 1000, CancellationToken cancellationToken = default)
        {
            var endpoint = Endpoint.Parse(host, port);
            var consumer = new NispConsumer(endpoint);

            if (!_consumers.TryAdd(endpoint, consumer))
            {
                _logger?.ZLogInformation($"[{DateTime.UtcNow}] The server is already listening on {host}:{port}");
                throw new InvalidOperationException($"The server is already listening on {host}:{port}");
            }

            bool successfullyStartedListening = false;

            while (!successfullyStartedListening && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await consumer.ListenAsync(cancellationToken).ConfigureAwait(false);
                    successfullyStartedListening = true;
                }
                catch (Exception)
                {
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }
            }

            if (successfullyStartedListening && _logger is not null)
                _logger.ZLogInformation($"[{DateTime.UtcNow}] The server has successfully started listening on {host}:{port}");
            else if (!successfullyStartedListening && _logger is not null)
                _logger.ZLogInformation($"[{DateTime.UtcNow}] The server failed to start listening on {host}:{port}");
        }

        public async Task StartClientListenerAsync(string host, int port, int delay = 1000, int sendTimeout = 30000, CancellationToken cancellationToken = default)
        {
            await StartListenerAsync(host, port, delay, cancellationToken).ConfigureAwait(false);
            await StartClientAsync(host, port, delay, sendTimeout, cancellationToken).ConfigureAwait(false);
        }

        public async IAsyncEnumerable<TMessage?> ReceiveAsync<TMessage>(string host, int port, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var endpoint = Endpoint.Parse(host, port);

            if (!_consumers.TryGetValue(endpoint, out var consumer))
            {
                _logger?.ZLogInformation($"[{DateTime.UtcNow}] The server is not listening on {endpoint.Host}:{endpoint.Port}");
                throw new InvalidOperationException($"The server is not listening on {endpoint.Host}:{endpoint.Port}");
            }

            while (!cancellationToken.IsCancellationRequested && consumer.IsConnected)
            {
                var message = await consumer.ReceiveAsync<TMessage>(cancellationToken).ConfigureAwait(false);

                if (message == null)
                    yield break;

                _logger?.ZLogInformation($"[{DateTime.UtcNow}] Received message of type {typeof(TMessage).Name} from {endpoint.Host}:{endpoint.Port}");

                yield return message;
            }
        }

        public async Task SendAsync<TMessage>(string host, int port, TMessage message, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(message);

            var endpoint = Endpoint.Parse(host, port);

            if (!_producers.TryGetValue(endpoint, out var producer))
            {
                _logger?.ZLogInformation($"[{DateTime.UtcNow}] The client is not connected to {endpoint.Host}:{endpoint.Port}");
                throw new InvalidOperationException($"The client is not connected to {endpoint.Host}:{endpoint.Port}");
            }

            await producer.SendAsync(message, cancellationToken).ConfigureAwait(false);

            _logger?.ZLogInformation($"[{DateTime.UtcNow}] Sent message of type {typeof(TMessage).Name} to {endpoint.Host}:{endpoint.Port}");
        }

        public async Task StopClientAsync(string host, int port)
        {
            var endpoint = Endpoint.Parse(host, port);

            if (!_producers.TryGetValue(endpoint, out var producer))
            {
                _logger?.ZLogInformation($"[{DateTime.UtcNow}] The client is not connected to {endpoint.Host}:{endpoint.Port}");
                throw new InvalidOperationException($"The client is not connected to {endpoint.Host}:{endpoint.Port}");
            }

            await producer.DisposeAsync().ConfigureAwait(false);
            _producers.Remove(endpoint);

            _logger?.ZLogInformation($"[{DateTime.UtcNow}] The client has successfully disconnected from {endpoint.Host}:{endpoint.Port}");
        }

        public async Task StopListenerAsync(string host, int port)
        {
            var endpoint = Endpoint.Parse(host, port);

            if (!_consumers.TryGetValue(endpoint, out var consumer))
            {
                _logger?.ZLogInformation($"[{DateTime.UtcNow}] The server is not listening on {endpoint.Host}:{endpoint.Port}");
                throw new InvalidOperationException($"The server is not listening on {endpoint.Host}:{endpoint.Port}");
            }

            await consumer.DisposeAsync().ConfigureAwait(false);
            _consumers.Remove(endpoint);

            _logger?.ZLogInformation($"[{DateTime.UtcNow}] The server has successfully stopped listening on {endpoint.Host}:{endpoint.Port}");
        }
    }
}