using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace certcentral.web.Account
{
    public class RegisterViewModel
    {
        [Required]
        public string Name { get; set; }
        [Required]
        public string Email { get; set; }

        public UserInfo ToUserInfo()
        {
            return new UserInfo
            {
                Login = Name,
                Email = Email,
                AccessKey = CreateCryptographicallySecureGuid()
            };
        }

        private Guid CreateCryptographicallySecureGuid()
        {
            using (var provider = System.Security.Cryptography.RandomNumberGenerator.Create())
            {
                var bytes = new byte[16];
                provider.GetBytes(bytes);

                return new Guid(bytes);
            }
        }
    }
}
