using EasyCompressor;
using Nisp.Core.Entities;
using Nisp.Core.Messages;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace Nisp.Core.Components
{
    /// <summary>
    /// Represents the base class for all NISP communication components.
    /// Provides common properties for network endpoints and connection state.
    /// </summary>
    public abstract class NispBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NispBase"/> class with the specified network and protocol configuration.
        /// </summary>
        /// <param name="ipAddress">The IP address of the remote endpoint or the local listening address.</param>
        /// <param name="port">The port number of the remote endpoint or the local listening port.</param>
        /// <param name="compressor">Optional compression provider for message payload compression/decompression.</param>
        /// <param name="sslOptions">Optional SSL/TLS configuration for secure connections.</param>
        /// <param name="messageTypes">Optional list of message type registrations with their unique identifiers.</param>
        /// <remarks>
        /// If messageTypes is null, the constructor automatically registers the default message types:
        /// <see cref="ImAliveMessage"/> with ID 1 and <see cref="AckMessage"/> with ID 2.
        /// </remarks>
        public NispBase(IPAddress ipAddress, int port, ICompressor? compressor, SslOptions? sslOptions, List<(ushort Id, Type Type)>? messageTypes)
        {
            IPAddress = ipAddress;
            Port = port;
            Compressor = compressor;
            SslOptions = sslOptions;

            if (messageTypes == null)
            {
                MessageTypes = [
                    (1, typeof(ImAliveMessage)),
                    (2, typeof(AckMessage))
                ];
            }
            else
            {
                MessageTypes = messageTypes;

                RegisterType<ImAliveMessage>();
                RegisterType<AckMessage>();
            }
        }

        /// <summary>
        /// Gets the IP address of the remote endpoint or the local listening address.
        /// </summary>
        /// <value>The IP address this component is connected to or listening on.</value>
        public IPAddress IPAddress { get; }

        /// <summary>
        /// Gets the port number of the remote endpoint or the local listening port.
        /// </summary>
        /// <value>The port number this component is connected to or listening on.</value>
        public int Port { get; }

        /// <summary>
        /// Gets a value indicating whether the component is currently connected to a remote peer.
        /// </summary>
        /// <value><c>true</c> if the connection is active and healthy; otherwise, <c>false</c>.</value>
        public abstract bool IsConnected { get; }

        protected ICompressor? Compressor { get; }

        protected SslOptions? SslOptions { get; }

        protected List<(ushort Id, Type Type)> MessageTypes { get; }

        protected abstract bool ValidateCertificate(object sender, X509Certificate? cert, X509Chain? chain, SslPolicyErrors errors);

        private void RegisterType<TMessage>()
        {
            if (!MessageTypes.Exists(t => t.Type == typeof(TMessage)))
            {
                int maxTag = MessageTypes.Max(t => t.Id);
                MessageTypes.Add(((ushort Id, Type Type))(maxTag + 1, typeof(TMessage)));
            }
        }
    }
}
