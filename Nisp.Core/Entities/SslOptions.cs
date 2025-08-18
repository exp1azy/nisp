using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace Nisp.Core.Entities
{
    public class SslOptions
    {
        public X509Certificate2 Certificate { get; set; }
        public bool ClientCertificateRequired { get; set; } = true;
        public bool CheckCertificateRevocation { get; set; } = true;
        public Func<object, X509Certificate?, X509Chain?, SslPolicyErrors, bool>? RemoteCertificateValidationCallback { get; set; }
    }
}
