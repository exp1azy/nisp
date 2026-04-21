namespace Nisp.Core.Entities
{
    /// <summary>
    /// Configuration settings for creating a bidirectional NISP peer connection.
    /// </summary>
    public class PeerConfig
    {
        /// <summary>
        /// Gets or sets the endpoint (host and port) for the outgoing client connection.
        /// </summary>
        /// <value>A tuple containing the hostname or IP address and the port number for the client connection.</value>
        /// <remarks>
        /// This endpoint is used by the <see cref="Components.NispSender"/> component to establish an outgoing connection to a remote peer.
        /// </remarks>
        public (string Host, int Port) SenderEndpoint { get; set; }

        /// <summary>
        /// Gets or sets the endpoint (host and port) for the incoming receiver listener.
        /// </summary>
        /// <value>A tuple containing the hostname or IP address and the port number for the receiver listener.</value>
        /// <remarks>
        /// This endpoint is used by the <see cref="Components.NispReceiver"/> component to listen for incoming connections.
        /// </remarks>
        public (string Host, int Port) ReceiverEndpoint { get; set; }

        /// <summary>
        /// Gets or sets the configuration for automatic ImAlive heartbeat messages.
        /// </summary>
        /// <value>An <see cref="Entities.ImAliveConfig"/> instance, or <c>null</c> if heartbeats are disabled.</value>
        /// <remarks>
        /// When configured, the peer will automatically send ImAlive messages at regular intervals to notify the remote peer of its health status.
        /// </remarks>
        public ImAliveConfig? ImAliveConfig { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether message acknowledgment is enabled for this peer.
        /// </summary>
        /// <value><c>true</c> if acknowledgment messages should be sent for every received message; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// When enabled, the peer will automatically send an <see cref="Messages.AckMessage"/> back to the sender peer for every successfully received message.
        /// </remarks>
        public bool Acknowledge { get; set; }

        /// <summary>
        /// Gets or sets the SSL/TLS configuration for the outgoing sender connection.
        /// </summary>
        /// <value>An <see cref="SslOptions"/> instance, or <c>null</c> if SSL/TLS is disabled for sender connections.</value>
        /// <remarks>
        /// If provided, the sender connection will use SSL/TLS encryption. This requires both the sender and receiver to have appropriate certificates configured.
        /// </remarks>
        public SslOptions? SenderSslOptions { get; set; }

        /// <summary>
        /// Gets or sets the SSL/TLS configuration for the incoming receiver connection.
        /// </summary>
        /// <value>An <see cref="SslOptions"/> instance, or <c>null</c> if SSL/TLS is disabled for the receiver.</value>
        /// <remarks>
        /// If provided, the receiver will require SSL/TLS encryption for incoming connections. This requires a valid receiver certificate to be configured.
        /// </remarks>
        public SslOptions? ReceiverSslOptions { get; set; }
    }
}
