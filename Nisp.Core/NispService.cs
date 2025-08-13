using Nisp.Core.Entities;

namespace Nisp.Core
{
    public class NispService
    {
        private readonly Dictionary<NispEndpoint, NispProducer> _producers = [];
        private readonly Dictionary<NispEndpoint, NispConsumer> _consumers = [];

        public async Task StartClientAsync(string host, int port, int delayInSecondsIfUnabledToConnect = 10, CancellationToken cancellationToken = default)
        {
            var endpoint = NispEndpoint.Parse(host, port);
            var producer = new NispProducer(endpoint);

            if (!_producers.TryAdd(endpoint, producer))
                throw new InvalidOperationException($"Client already connected to {host}:{port}");

            bool successfullyConnected = false;
            int retry = 0;

            while (!successfullyConnected && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await producer.ConnectAsync(cancellationToken).ConfigureAwait(false);
                    successfullyConnected = true;
                }
                catch (Exception)
                {
                    await Task.Delay(TimeSpan.FromSeconds(delayInSecondsIfUnabledToConnect), cancellationToken).ConfigureAwait(false);
                }
            }
        }

        public async Task StartListenerAsync(string host, int port, int delayInSecondsIfUnabledToListening = 10, CancellationToken cancellationToken = default)
        {
            var endpoint = NispEndpoint.Parse(host, port);
            var consumer = new NispConsumer(endpoint);
            
            if (!_consumers.TryAdd(endpoint, consumer))
                throw new InvalidOperationException($"Server already listening from {host}:{port}");

            bool successfullyStartedListening = false;

            while (!successfullyStartedListening && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await consumer.ConnectAsync(cancellationToken).ConfigureAwait(false);
                    successfullyStartedListening = true;
                }
                catch (Exception)
                {
                    await Task.Delay(TimeSpan.FromSeconds(delayInSecondsIfUnabledToStartListening), cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }
}
