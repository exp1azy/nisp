using System.Security.Cryptography.X509Certificates;

namespace Nisp.Core.Entities
{
    public class SslOptions
    {
        public X509Certificate2? Certificate { get; set; }
        public bool ClientCertificateRequired { get; set; }
        public bool CheckCertificateRevocation { get; set; }
    }
}
