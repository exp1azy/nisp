namespace Nisp.Core.Entities
{
    internal readonly struct NispEndpoint(string host, int port)
    {
        public string Host { get; } = host;
        public int Port { get; } = port;

        public static NispEndpoint Parse(string host, int port)
        {
            ArgumentNullException.ThrowIfNullOrEmpty(host);
            ArgumentOutOfRangeException.ThrowIfLessThan(port, 0);

            return new NispEndpoint(host, port);
        }
    }
}
