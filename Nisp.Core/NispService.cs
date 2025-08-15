using Microsoft.Extensions.Logging;
using Nisp.Core.Entities;

namespace Nisp.Core
{
    public class NispService
    {
        private ILogger<NispService>? _logger;
        private bool _enableCompression = false;

        public NispService WithLogging(Action<ILoggingBuilder> builder)
        {
            var loggerFactory = LoggerFactory.Create(builder);
            _logger = loggerFactory.CreateLogger<NispService>();
            return this;
        }

        public NispService WithCompression()
        {
            _enableCompression = true;
            return this;
        }

        public NispClient CreateClient(string host, int port)
        {
            ArgumentNullException.ThrowIfNullOrEmpty(host);
            ArgumentOutOfRangeException.ThrowIfLessThan(port, 0);

            return new NispClient(host, port, _enableCompression, _logger);
        }

        public NispListener CreateListener(string host, int port)
        {
            ArgumentNullException.ThrowIfNullOrEmpty(host);
            ArgumentOutOfRangeException.ThrowIfLessThan(port, 0);

            return new NispListener(host, port, _enableCompression, _logger);
        }

        public NispPeer CreatePeer(string localHost, int localPort, string remoteHost, int remotePort)
        {
            ArgumentNullException.ThrowIfNullOrEmpty(localHost);
            ArgumentOutOfRangeException.ThrowIfLessThan(localPort, 0);
            ArgumentNullException.ThrowIfNullOrEmpty(remoteHost);
            ArgumentOutOfRangeException.ThrowIfLessThan(remotePort, 0);

            return new NispPeer(CreateClient(localHost, localPort), CreateListener(remoteHost, remotePort));
        }
    }
}