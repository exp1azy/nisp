using EasyCompressor;
using Microsoft.Extensions.Logging;
using Nisp.Core.Components;
using Nisp.Core.Compressors;
using Nisp.Core.Entities;
using System.Net;

namespace Nisp.Core
{
    /// <summary>
    /// Main entry point for creating NISP communication components.
    /// </summary>
    public class NispService
    {
        private ILoggerFactory? _loggerFactory;
        private ICompressor? _compressor;
        private List<(ushort Id, Type Type)>? _messageTypes;

        /// <summary>
        /// Configures logging for all created NISP components.
        /// </summary>
        /// <param name="builder">A delegate that configures the logging builder.</param>
        /// <returns>The current <see cref="NispService"/> instance for method chaining.</returns>
        public NispService WithLogging(Action<ILoggingBuilder> builder)
        {
            _loggerFactory = LoggerFactory.Create(builder);
            return this;
        }

        /// <summary>
        /// Configures compression for all created NISP components.
        /// </summary>
        /// <param name="action">A delegate that receives an <see cref="ICompressorFactory"/> and returns an <see cref="ICompressor"/> instance.</param>
        /// <returns>The current <see cref="NispService"/> instance for method chaining.</returns>
        /// <remarks>
        /// The factory supports:
        /// <list type="bullet">
        /// <item><description><c>UseZstdSharp()</c> - ZstdSharpCompressor</description></item>
        /// <item><description><c>UseLZ4()</c> - LZ4Compressor</description></item>
        /// <item><description><c>UseSnappier()</c> - SnappierCompressor</description></item>
        /// </list>
        /// </remarks>
        public NispService WithCompression(Func<ICompressorFactory, ICompressor> action)
        {
            var compressorFactory = new CompressorFactory();
            _compressor = action(compressorFactory);

            return this;
        }

        /// <summary>
        /// Registers message types with their unique identifiers for serialization.
        /// </summary>
        /// <param name="messageTypes">A list of tuples containing (Id, Type) for each message type.</param>
        /// <returns>The current <see cref="NispService"/> instance for method chaining.</returns>
        /// <remarks>
        /// Message types must be registered with a unique ushort identifier (1-65535).
        /// These IDs are used in the protocol header to identify the message type for deserialization.
        /// Both communicating parties must use the same ID-to-Type mappings.
        /// </remarks>
        public NispService WithMessageTypes(params List<(ushort Id, Type Type)> messageTypes)
        {
            _messageTypes = messageTypes;
            return this;
        }

        /// <summary>
        /// Creates a new NISP sender component for sending messages to a remote endpoint.
        /// </summary>
        /// <param name="host">Target hostname or IP address.</param>
        /// <param name="port">Target port number (1-65535).</param>
        /// <param name="sslOptions">Optional SSL/TLS configuration for secure connections.</param>
        /// <returns>A configured <see cref="NispSender"/> instance ready for connection.</returns>
        /// <exception cref="ArgumentException">Thrown when host is null or empty.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when port is less than 0.</exception>
        public NispSender CreateSender(string host, int port, SslOptions? sslOptions = null)
        {
            ArgumentException.ThrowIfNullOrEmpty(host);
            ArgumentOutOfRangeException.ThrowIfLessThan(port, 0);

            var ipAddress = IPAddress.Parse(host);

            return new NispSender(ipAddress, port, _compressor, sslOptions, _messageTypes, _loggerFactory);
        }

        /// <summary>
        /// Creates a new NISP receiver component for listening to incoming connections.
        /// </summary>
        /// <param name="host">Hostname or IP address to listen on.</param>
        /// <param name="port">Port number to listen on (1-65535).</param>
        /// <param name="sslOptions">Optional SSL/TLS configuration for secure connections.</param>
        /// <returns>A configured <see cref="NispReceiver"/> instance ready for listening.</returns>
        /// <exception cref="ArgumentException">Thrown when host is null or empty.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when port is less than 0.</exception>
        public NispReceiver CreateReceiver(string host, int port, SslOptions? sslOptions = null)
        {
            ArgumentException.ThrowIfNullOrEmpty(host);
            ArgumentOutOfRangeException.ThrowIfLessThan(port, 0);

            var ipAddress = IPAddress.Parse(host);

            return new NispReceiver(ipAddress, port, _compressor, sslOptions, _messageTypes, _loggerFactory);
        }

        /// <summary>
        /// Creates a new bidirectional NISP peer that combines both sender and receiver capabilities.
        /// </summary>
        /// <param name="config">Peer configuration containing endpoints, SSL options, and heartbeat settings.</param>
        /// <returns>A configured <see cref="NispPeer"/> instance ready for bidirectional communication.</returns>
        /// <exception cref="ArgumentNullException">Thrown when config is null or any endpoint host is null/empty.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when any port is less than 0.</exception>
        public NispPeer CreatePeer(PeerConfig config)
        {
            ArgumentNullException.ThrowIfNull(config);
            ArgumentException.ThrowIfNullOrEmpty(config.SenderEndpoint.Host);
            ArgumentOutOfRangeException.ThrowIfLessThan(config.SenderEndpoint.Port, 0);
            ArgumentException.ThrowIfNullOrEmpty(config.ReceiverEndpoint.Host);
            ArgumentOutOfRangeException.ThrowIfLessThan(config.ReceiverEndpoint.Port, 0);

            var clientAddress = IPAddress.Parse(config.SenderEndpoint.Host);
            var receiverAddress = IPAddress.Parse(config.ReceiverEndpoint.Host);

            return new NispPeer(
                new NispSender(clientAddress, config.SenderEndpoint.Port, _compressor, config.SenderSslOptions, _messageTypes, _loggerFactory),
                new NispReceiver(receiverAddress, config.ReceiverEndpoint.Port, _compressor, config.ReceiverSslOptions, _messageTypes, _loggerFactory),
                config.ImAliveConfig,
                config.Acknowledge
            );
        }
    }
}