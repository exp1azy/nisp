namespace Nisp.Core.Entities
{
    /// <summary>
    /// Specifies how the NISP receiver should handle errors that occur during message processing.
    /// </summary>
    public enum ReceiveErrorBehavior
    {
        /// <summary>
        /// Log the error and continue processing subsequent messages. The faulty message is skipped.
        /// </summary>
        Ignore,

        /// <summary>
        /// Stop the receive loop gracefully without throwing an exception. The receiver will no longer process messages.
        /// </summary>
        Stop,

        /// <summary>
        /// Stop the receive loop and rethrow the exception to the caller. This is the default behavior.
        /// </summary>
        StopAndThrow
    }
}
