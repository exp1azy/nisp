using MemoryPack;

namespace Nisp.Core.Entities
{
    /// <summary>
    /// Represents a heartbeat message sent between peers to indicate that a service is still alive and responsive.
    /// </summary>
    /// <remarks>
    /// ImAlive messages are automatically sent at regular intervals when configured in <see cref="ImAliveConfig"/>.
    /// </remarks>
    [MemoryPackable]
    public partial class ImAliveMessage
    {
        /// <summary>
        /// Gets or sets the unique identifier of the peer sending the heartbeat.
        /// </summary>
        /// <value>A <see cref="Guid"/> that uniquely identifies the source peer instance.</value>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the heartbeat message was created.
        /// </summary>
        /// <value>The UTC date and time when the message was generated.</value>
        public DateTime DateTime { get; set; }
    }
}
