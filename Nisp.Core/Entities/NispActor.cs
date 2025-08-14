namespace Nisp.Core.Entities
{
    public abstract class NispActor : IAsyncDisposable
    {
        public abstract bool IsConnected { get; }
        public string TargetHost { get; protected set; }
        public int TargetPort { get; protected set; }

        public abstract ValueTask DisposeAsync();
    }
}
