using MemoryPack;

namespace Nisp.Core.Messages
{
    /// <summary>
    /// Represents an acknowledgment message sent by a peer to confirm receipt of a message.
    /// </summary>
    /// <remarks>
    /// When acknowledgment mode is enabled on a peer, an <see cref="AckMessage"/> is automatically sent back to the sender peer for every received message.
    /// </remarks>
    [MemoryPackable]
    public partial class AckMessage
    {
        /// <summary>
        /// Gets or sets the unique identifier of the peer sending the acknowledgment.
        /// </summary>
        /// <value>The <see cref="Guid"/> of the receiving peer.</value>
        public Guid PeerId { get; set; }

        /// <summary>
        /// Gets or sets the type name of the message being acknowledged.
        /// </summary>
        /// <value>The full name or assembly-qualified name of the acknowledged message type.</value>
        public string TypeReceived { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the timestamp when the acknowledgment was created.
        /// </summary>
        /// <value>The UTC date and time of acknowledgment generation.</value>
        public DateTime DateTime { get; set; }
    }
}
