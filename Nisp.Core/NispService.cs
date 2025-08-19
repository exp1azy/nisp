using Microsoft.Extensions.Logging;
using Nisp.Core.Components;

namespace Nisp.Core
{
    /// <summary>
    /// A class for creating NISP communication components (clients, listeners, and peers)
    /// with configurable logging, compression, and SSL settings.
    /// </summary>
    public class NispService
    {
        private ILogger<NispService>? _logger;
        private bool _enableCompression;

        /// <summary>
        /// Configures ZLogger logging for all created NISP components.
        /// </summary>
        /// <param name="builder">Action to configure the logging builder.</param>
        /// <returns>The current <see cref="NispService"/> instance for method chaining.</returns>
        public NispService WithLogging(Action<ILoggingBuilder> builder)
        {
            var loggerFactory = LoggerFactory.Create(builder);
            _logger = loggerFactory.CreateLogger<NispService>();
            return this;
        }

        /// <summary>
        /// Enables LZ4 compression for all created NISP components.
        /// </summary>
        /// <returns>The current <see cref="NispService"/> instance for method chaining.</returns>
        /// <remarks>
        /// When enabled, all messages will be compressed using LZ4 algorithm before transmission.
        /// Both communicating parties must have compression enabled for proper operation.
        /// </remarks>
        public NispService WithCompression()
        {
            _enableCompression = true;
            return this;
        }

        /// <summary>
        /// Creates a new NISP client instance with current configuration.
        /// </summary>
        /// <param name="host">Target hostname or IP address.</param>
        /// <param name="port">Target port number.</param>
        /// <param name="sslOptions">SSL/TLS configuration options.</param>
        /// <returns>Configured <see cref="NispClient"/> instance.</returns>
        /// <exception cref="ArgumentNullException">Thrown if host is null or empty.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if port is less than 0.</exception>
        public NispClient CreateClient(string host, int port, SslOptions? sslOptions = null)
        {
            ArgumentNullException.ThrowIfNullOrEmpty(host);
            ArgumentOutOfRangeException.ThrowIfLessThan(port, 0);

            return new NispClient(host, port, _enableCompression, sslOptions, _logger);
        }

        /// <summary>
        /// Creates a new NISP listener instance with current configuration.
        /// </summary>
        /// <param name="host">Hostname or IP address to listen on.</param>
        /// <param name="port">Port number to listen on.</param>
        /// <param name="sslOptions">SSL/TLS configuration options.</param>
        /// <returns>Configured <see cref="NispListener"/> instance.</returns>
        /// <exception cref="ArgumentNullException">Thrown if host is null or empty.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if port is less than 0.</exception>
        public NispListener CreateListener(string host, int port, SslOptions? sslOptions = null)
        {
            ArgumentNullException.ThrowIfNullOrEmpty(host);
            ArgumentOutOfRangeException.ThrowIfLessThan(port, 0);

            return new NispListener(host, port, _enableCompression, sslOptions, _logger);
        }

        /// <summary>
        /// Creates a new bidirectional NISP peer with current configuration.
        /// </summary>
        /// <param name="config">Peer configuration parameters.</param>
        /// <returns>Configured <see cref="NispPeer"/> instance.</returns>
        /// <exception cref="ArgumentNullException">Thrown if any host parameter is null or empty.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if any port parameter is less than 0.</exception>
        /// <remarks>
        /// The peer combines both client and listener capabilities for full-duplex communication.
        /// Both components will share the same configuration (compression, encryption, logging).
        /// </remarks>
        public NispPeer CreatePeer(PeerConfig config)
        {
            ArgumentNullException.ThrowIfNull(config);
            ArgumentNullException.ThrowIfNullOrEmpty(config.ClientHost);
            ArgumentOutOfRangeException.ThrowIfLessThan(config.ClientPort, 0);
            ArgumentNullException.ThrowIfNullOrEmpty(config.ListenerHost);
            ArgumentOutOfRangeException.ThrowIfLessThan(config.ListenerPort, 0);

            return new NispPeer(
                new NispClient(config.ClientHost, config.ClientPort, _enableCompression, config.ClientSslOptions, _logger),
                new NispListener(config.ListenerHost, config.ListenerPort, _enableCompression, config.ListenerSslOptions, _logger)
            );
        }
    }
}