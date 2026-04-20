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
        /// Gets the IP address of the remote endpoint or the local listening address.
        /// </summary>
        /// <value>The IP address this component is connected to or listening on.</value>
        public abstract IPAddress IPAddress { get; }

        /// <summary>
        /// Gets the port number of the remote endpoint or the local listening port.
        /// </summary>
        /// <value>The port number this component is connected to or listening on.</value>
        public abstract int Port { get; }

        /// <summary>
        /// Gets a value indicating whether the component is currently connected to a remote peer.
        /// </summary>
        /// <value><c>true</c> if the connection is active and healthy; otherwise, <c>false</c>.</value>
        public abstract bool IsConnected { get; }

        protected abstract bool ValidateCertificate(object sender, X509Certificate? cert, X509Chain? chain, SslPolicyErrors errors);
    }
}
