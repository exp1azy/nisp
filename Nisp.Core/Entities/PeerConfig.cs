namespace Nisp.Core.Entities
{
    public class PeerConfig
    {
        public string ClientHost { get; set; }
        public int ClientPort { get; set; }
        public string ListenerHost { get; set; }
        public int ListenerPort { get; set; }
        public SslOptions? ClientSslOptions { get; set; }
        public SslOptions? ListenerSslOptions { get; set; }
    }
}
