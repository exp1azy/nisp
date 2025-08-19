namespace Nisp.Core.Components
{
    /// <summary>
    /// Represents configuration settings for a peer.
    /// </summary>
    /// <remarks>
    /// The peer combines both client and listener capabilities for full-duplex communication.
    /// </remarks>
    public class PeerConfig
    {
        /// <summary>
        /// Gets or sets the host address for the client connection.
        /// </summary>
        public string ClientHost { get; set; }

        /// <summary>
        /// Gets or sets the port number for the client connection.
        /// </summary>
        public int ClientPort { get; set; }

        /// <summary>
        /// Gets or sets the host address for the listener connection.
        /// </summary>
        public string ListenerHost { get; set; }

        /// <summary>
        /// Gets or sets the port number for the listener connection.
        /// </summary>
        public int ListenerPort { get; set; }

        /// <summary>
        /// Gets or sets the SSL/TLS options for the client-side connection.
        /// </summary>
        /// <remarks>
        /// Specify, if you want to use SSL/TLS for the client-side connection.
        /// </remarks>
        public SslOptions? ClientSslOptions { get; set; }

        /// <summary>
        /// Gets or sets the SSL/TLS options for the listener (server-side) connection.
        /// </summary>
        public SslOptions? ListenerSslOptions { get; set; }
    }
}
