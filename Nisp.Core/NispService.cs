using Microsoft.Extensions.Logging;
using Nisp.Core.Components;

namespace Nisp.Core
{
    /// <summary>
    /// A class for creating NISP communication components (clients, receivers, and peers)
    /// with configurable logging, compression, and SSL settings.
    /// </summary>
    public class NispService
    {
        private ILoggerFactory? _loggerFactory;
        private int? _compressionLevel;

        /// <summary>
        /// Configures logging for all created NISP components.
        /// </summary>
        /// <param name="builder">Logging builder.</param>
        /// <returns>The current <see cref="NispService"/> instance for method chaining.</returns>
        public NispService WithLogging(Action<ILoggingBuilder> builder)
        {
            _loggerFactory = LoggerFactory.Create(builder);
            return this;
        }

        /// <summary>
        /// Enables ZStandard compression for all created NISP components.
        /// </summary>
        /// <param name="level">Level of comression.</param>
        /// <returns>The current <see cref="NispService"/> instance for method chaining.</returns>
        /// <remarks>
        /// When enabled, all messages will be compressed using ZStandard algorithm before transmission.
        /// Both communicating parties must have compression enabled for proper operation.
        /// </remarks>
        public NispService WithCompression(int level = 3)
        {
            _compressionLevel = level;
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
            ArgumentException.ThrowIfNullOrEmpty(host);
            ArgumentOutOfRangeException.ThrowIfLessThan(port, 0);

            return new NispClient(host, port, _compressionLevel, sslOptions, _loggerFactory);
        }

        /// <summary>
        /// Creates a new NISP receiver instance with current configuration.
        /// </summary>
        /// <param name="host">Hostname or IP address to listen on.</param>
        /// <param name="port">Port number to listen on.</param>
        /// <param name="sslOptions">SSL/TLS configuration options.</param>
        /// <returns>Configured <see cref="NispReceiver"/> instance.</returns>
        /// <exception cref="ArgumentNullException">Thrown if host is null or empty.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if port is less than 0.</exception>
        public NispReceiver CreateReceiver(string host, int port, SslOptions? sslOptions = null)
        {
            ArgumentException.ThrowIfNullOrEmpty(host);
            ArgumentOutOfRangeException.ThrowIfLessThan(port, 0);

            return new NispReceiver(host, port, _compressionLevel != null, sslOptions, _loggerFactory);
        }

        /// <summary>
        /// Creates a new bidirectional NISP peer with current configuration.
        /// </summary>
        /// <param name="config">Peer configuration parameters.</param>
        /// <returns>Configured <see cref="NispPeer"/> instance.</returns>
        /// <exception cref="ArgumentNullException">Thrown if any host parameter is null or empty.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if any port parameter is less than 0.</exception>
        /// <remarks>
        /// The peer combines both client and receiver capabilities for full-duplex communication.
        /// Both components will share the same configuration (compression, encryption, logging).
        /// </remarks>
        public NispPeer CreateBidirectional(PeerConfig config)
        {
            ArgumentNullException.ThrowIfNull(config);
            ArgumentException.ThrowIfNullOrEmpty(config.ClientHost);
            ArgumentOutOfRangeException.ThrowIfLessThan(config.ClientPort, 0);
            ArgumentException.ThrowIfNullOrEmpty(config.ListenerHost);
            ArgumentOutOfRangeException.ThrowIfLessThan(config.ListenerPort, 0);

            return new NispPeer(
                new NispClient(config.ClientHost, config.ClientPort, _compressionLevel, config.ClientSslOptions, _loggerFactory),
                new NispReceiver(config.ListenerHost, config.ListenerPort, _compressionLevel != null, config.ListenerSslOptions, _loggerFactory)
            );
        }
    }
}