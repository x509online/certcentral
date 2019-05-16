using certcentral.web.Controllers;
using System;
using System.Security.Cryptography.X509Certificates;
using Xunit;

namespace certcentral.tests
{
    public class CertInfoTests
    {
        [Fact]
        public void SHA256()
        {
            X509Store store = new X509Store();
            store.Open(OpenFlags.ReadOnly);
            var c = store.Certificates[0];
            var ci = CertInfo.FromX509(c);
            Assert.False(string.IsNullOrEmpty(ci.SHA256));
            Console.WriteLine(ci.SHA256);

        }
    }
}
