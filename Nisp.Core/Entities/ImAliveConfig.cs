namespace Nisp.Core.Entities
{
    /// <summary>
    /// Configuration settings for automatic ImAlive heartbeat message sending.
    /// </summary>
    /// <remarks>
    /// This class controls how often a peer sends heartbeat messages to notify remote peers about its health status.
    /// </remarks>
    public class ImAliveConfig
    {
        /// <summary>
        /// Gets or sets the interval between consecutive ImAlive heartbeat messages.
        /// </summary>
        /// <value>The delay in milliseconds between sending heartbeat messages.</value>
        public int DelayInMilliseconds { get; set; }
    }
}
