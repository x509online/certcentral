using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace certcentral.web.Account
{
    public class UserInfo
    {
        public UserInfo()
        {
            CertFileNames = new Dictionary<string, string>();
        }
        public string Login { get; set; }
        public string Email { get; set; }
        public string RandomGuid { get; set; }
        public Guid AccessKey { get; set; }
        public Dictionary<string, string> CertFileNames { get; set; }
    }
}
