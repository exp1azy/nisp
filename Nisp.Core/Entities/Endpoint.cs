namespace Nisp.Core.Entities
{
    public readonly struct Endpoint(string host, int port) : IEquatable<Endpoint>
    {
        public string Host { get; } = host;
        public int Port { get; } = port;

        public static Endpoint Parse(string host, int port)
        {
            ArgumentNullException.ThrowIfNullOrEmpty(host);
            ArgumentOutOfRangeException.ThrowIfLessThan(port, 0);

            return new Endpoint(host, port);
        }

        public bool Equals(Endpoint other)
        {
            return Host == other.Host && Port == other.Port;
        }

        public override bool Equals(object? obj)
        {
            return obj is Endpoint nispEndpoint && Equals(nispEndpoint);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Host, Port);
        }

        public static bool operator ==(Endpoint left, Endpoint right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Endpoint left, Endpoint right)
        {
            return !(left == right);
        }
    }
}
