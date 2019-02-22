using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using certcentral.web.Storage;

namespace certcentral.web.Account
{
    public class AccountService
    {
        private readonly StorageProvider _storageProvider;

        public AccountService(StorageProvider storageProvider)
        {
            _storageProvider = storageProvider;
        }

        public async Task<bool> IsRegisteredAsync(string name)
        {
            return await _storageProvider.BlobExistsAsync(GetUserProfileBlobName(name));
        }

        public async Task RegisterAsync(UserInfo userInfo)
        {
            //TODO: VALIDATE USERINFO
            var content = JsonConvert.SerializeObject(userInfo);
            await _storageProvider.CreateOrUpdateAsync(content, GetUserProfileBlobName(userInfo.Login));
        }

        public async Task UnregisterAsync(string name)
        {
            //TODO: Delete all certificates
            await _storageProvider.DeleteAsync(GetUserProfileBlobName(name));
        }

        public async Task UploadCertAsync(IFormFile certFile, string userName)
        {
            using (var s = certFile.OpenReadStream())
            {

                await _storageProvider.CreateOrUpdateAsync(s, GetUserCertBlobName(userName, "temp"));
            }
        }

        public async Task UploadCertAsync(X509Certificate2 cert, string userName)
        {
            await _storageProvider.CreateOrUpdateAsync(Convert.ToBase64String(cert.RawData), GetUserCertBlobName(userName, cert.Thumbprint));
        }


        public async Task<X509Certificate2> GetUserCertAsync(string userName,string thumbprint)
        {
            var certBytes = await _storageProvider.ReadBlobAsync(GetUserCertBlobName(userName, thumbprint));
            return new X509Certificate2(certBytes);
        }

        private string GetUserProfileBlobName(string name) => $"{name}/profile.json";
        private string GetUserCertBlobName(string name, string thumbprint) => $"{name}/{thumbprint.ToUpperInvariant()}.cer";

        public async Task<UserInfo> GetUserInfo(string name)
        {
            if (await _storageProvider.BlobExistsAsync(GetUserProfileBlobName(name)))
            {
                var bytes = await _storageProvider.ReadBlobAsync(GetUserProfileBlobName(name));
                string json = System.Text.Encoding.UTF8.GetString(bytes);
                var res = JsonConvert.DeserializeObject<UserInfo>(json);
                if (res.CertFileNames == null)
                {
                    res.CertFileNames = new Dictionary<string, string>();
                }
                return res;
            }
            else
            {
                return null;
            }
        }

        internal async Task DeleteAsync(string username, string thumbprint)
        {
            string path = $"{username}/{thumbprint.ToUpperInvariant()}.cer";
            await _storageProvider.DeleteAsync(path);
        }
    }
}
