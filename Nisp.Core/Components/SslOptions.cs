using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace Nisp.Core.Components
{
    /// <summary>
    /// Represents SSL/TLS configuration options.
    /// </summary>
    public class SslOptions
    {
        /// <summary>
        /// Gets or sets the X.509 certificate to use for SSL/TLS authentication.
        /// </summary>
        public X509Certificate2 Certificate { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether client certificate validation is required during authentication.
        /// </summary>
        public bool ClientCertificateRequired { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether certificate revocation checks are enabled during authentication.
        /// </summary>
        public bool CheckCertificateRevocation { get; set; } = true;

        /// <summary>
        /// Gets or sets a callback function for custom certificate validation during authentication.
        /// </summary>
        public Func<object, X509Certificate?, X509Chain?, SslPolicyErrors, bool>? RemoteCertificateValidationCallback { get; set; }
    }
}
