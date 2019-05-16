using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using certcentral.web.Account;
using certcentral.web.Storage;
using System.Security.Cryptography.Pkcs;
using System.Text;
using Microsoft.WindowsAzure.Storage.Blob;

namespace certcentral.web.Controllers
{

    public class CertsDictionary : Dictionary<string, string> { }

    public class CertInfo
    {
        public string SubjectName { get; set; }
        public string ThumbPrint { get; set; }
        public string Issuer { get; set; }
        public string SHA256 { get; set; }

        public static CertInfo FromX509(X509Certificate2 c)
        {
            return new CertInfo
            {
                SubjectName = c.SubjectName.Name,
                Issuer = c.Issuer,
                ThumbPrint = c.Thumbprint.ToUpperInvariant(),
                SHA256 = c.GetCertHashString(new System.Security.Cryptography.HashAlgorithmName("SHA256"))
            };
        }
    }

    [Route("api/[controller]")]
    [ApiController]
    public class CertController : ControllerBase
    {
        // GET: api/Cert
        private readonly AccountService _accountService;
        private readonly StorageProvider _storageProvider;
        private readonly IHttpContextAccessor _httpContext;
        private IMemoryCache _cache;
        private CertsDictionary _certsByUser;

        TelemetryClient tc;
        public CertController(AccountService accountService, StorageProvider storageProvider, IHttpContextAccessor httpCtx, IMemoryCache memcache)
        {
            _cache = memcache;
            _storageProvider = storageProvider;
            _accountService = accountService;
            _httpContext = httpCtx;
            tc = new TelemetryClient();
            LoadCertsFromBlobs().Wait();
        }

        // GET: api/Cert
        [HttpGet()]
        public async Task<IEnumerable<X509Certificate2>> Get()
        {
            tc.TrackEvent("Get certs by user");
            var auth = ParseHeaders();
            var user = await _accountService.GetUserInfo(auth.name);

            if (!auth.key.Equals(user.AccessKey))
            {
                throw new SecurityException("Invalid Key");
            }

            List<X509Certificate2> userCerts = new List<X509Certificate2>();
            foreach (var item in user.CertFileNames)
            {
                tc.TrackTrace("loading cert:" + item.Key);
                var c = await _accountService.GetUserCertAsync(auth.name, item.Key);
                userCerts.Add(c);
            }
            return userCerts;
        }

        // GET: api/Cert/5
        //[HttpGet("Get")] //GetMyCertsByThumbprint
        //public async Task<X509Certificate2> Get(string thumbprint)
        //{
        //    tc.TrackEvent("Get Cert by thumbprint:" + thumbprint);

        //    var auth = ParseHeaders();
        //    var user = await _accountService.GetUserInfo(auth.name); 

        //    if (!auth.key.Equals(user.AccessKey))
        //    {
        //        throw new SecurityException("Invalid Key");
        //    }

        //    var cert = await _accountService.GetUserCertAsync(auth.name, thumbprint); 

        //    return cert;
        //}

        [HttpGet("GetUserCert")]
        public async Task<X509Certificate2> GetUserCert(string username, string thumbprint)
        {
            tc.TrackEvent("Get Cert by username and thumbprint:" + thumbprint);
            var cert = await _accountService.GetUserCertAsync(username, thumbprint);
            return cert;
        }

        [HttpGet("DownloadCert")]
        public async Task<IActionResult> DownloadCert(string username, string thumbprint)
        {
            var cert = await GetUserCert(username, thumbprint);
            var raw = cert.GetRawCertData();
            return File(raw, "application/x-x509-ca-cert", thumbprint+".cer");
        }

        // Get: api/Cert/rnd
        [HttpGet("rnd")]
        public async Task<string> GetRandomString()
        {
            tc.TrackEvent("GetRandomString");
            var auth = ParseHeaders();
            var user = await _accountService.GetUserInfo(auth.name);

            if (!auth.key.Equals(user.AccessKey))
            {
                throw new SecurityException("Invalid Key");
            }

            Random rnd = new Random(Environment.TickCount);
            var randomguid = rnd.Next(int.MaxValue).ToString();
            user.RandomGuid = randomguid;
            await _accountService.RegisterAsync(user);
            return randomguid;
        }

        // Get: api/Cert/Verify
        [HttpGet("Verify")]
        public async Task<X509Certificate2> Verify(string data, string signature)
        {
            tc.TrackTrace($"StartVerify DATA:{data} Sign:{signature}");

            var auth = ParseHeaders();
            var user = await _accountService.GetUserInfo(auth.name);

            if (!auth.key.Equals(user.AccessKey))
            {
                throw new SecurityException("Invalid Key");
            }

            if (data != user.RandomGuid)
            {
                throw new SecurityException($"Randmon data does not match: \n {data} \n {user.RandomGuid}");
            }

            try
            {
                var signedCms = new SignedCms(new ContentInfo(Encoding.UTF8.GetBytes(data)), true);
                signedCms.Decode(Convert.FromBase64String(signature));
                signedCms.CheckSignature(true);
                var cert = signedCms.SignerInfos[0].Certificate;

                if (_certsByUser.ContainsKey(cert.Thumbprint))
                {
                    var c = _certsByUser[cert.Thumbprint];
                    throw new ApplicationException("Certificate already registered for user:" + c);
                }

                bool exists = await _storageProvider.BlobExistsAsync($"{auth.name}/{cert.Thumbprint.ToUpperInvariant()}.cer");
                if (!exists)
                {
                    await _accountService.UploadCertAsync(cert, auth.name);
                    user.RandomGuid = string.Empty;
                }

                if (!user.CertFileNames.ContainsKey(cert.Thumbprint))
                {
                    user.CertFileNames.Add(cert.Thumbprint, cert.SubjectName.Name);
                }
                await _accountService.RegisterAsync(user);

                _certsByUser.Add(cert.Thumbprint, user.Login);
                _cache.Set("certsByUser", _certsByUser);
                return cert;
            }
            catch (Exception ex)
            {
                Console.WriteLine("EXCEPTION");
                Console.WriteLine(ex.ToString());
                tc.TrackException(ex);
                throw ex;
            }
        }

        [HttpGet("deletecert/{thumbprint}")]
        public async Task DeleteCert(string thumbprint)
        {
            var auth = ParseHeaders();
            var user = await _accountService.GetUserInfo(auth.name);

            if (!auth.key.Equals(user.AccessKey))
            {
                throw new SecurityException("Invalid Key");
            }

            if (user.CertFileNames.ContainsKey(thumbprint))
            {
                await _storageProvider.DeleteAsync($"{auth.name}/{thumbprint.ToUpperInvariant()}.cer");
                user.CertFileNames.Remove(thumbprint);
                await _accountService.RegisterAsync(user);
                _certsByUser.Remove(thumbprint);
                _cache.Set("certsByUser", _certsByUser);
            }
        }

        [HttpGet("Login")]
        public async Task Login()
        {
            var auth = ParseHeaders();
            var user = await _accountService.GetUserInfo(auth.name);

            if (!auth.key.Equals(user.AccessKey))
            {
                throw new SecurityException("Invalid Key");
            }
            return;
        }


        [HttpGet("GetCertsFromUser/{username}")] //Anonymous
        public async Task<IEnumerable<CertInfo>> GetCertsFromUser(string username)
        {
            var user = await _accountService.GetUserInfo(username);
            if (user == null)
            {
                return null;
            }

            List<CertInfo> userCerts = new List<CertInfo>();
            foreach (var item in user.CertFileNames)
            {
                tc.TrackTrace("loading cert:" + item.Key);
                var c = await _accountService.GetUserCertAsync(username, item.Key);
                userCerts.Add(CertInfo.FromX509(c));
            }
            return userCerts;
        }

        [HttpGet("GetUsers")]
        public ActionResult<IEnumerable<string>> GetUsers()
        {
            return _certsByUser.Values.Distinct().ToArray<string>();
        }

        [HttpGet("User/{userName}")]
        public ActionResult<IEnumerable<KeyValuePair<string, string>>> GetByUser(string userName)
        {
            return _certsByUser.Where(c => c.Value == userName).ToArray<KeyValuePair<string, string>>();
        }

        [HttpGet("Thumbprint/{thumbprint}")]
        public ActionResult<string> GetByThumbprint(string thumbprint)
        {
            if (_certsByUser.ContainsKey(thumbprint.ToUpperInvariant()))
            {
                return _certsByUser[thumbprint.ToUpperInvariant()];
            }
            else
            {
                return NotFound();
            }
        }

        [HttpGet("ThumbprintStartsWith/{thumbprintStart}")]
        public ActionResult<IEnumerable<KeyValuePair<string, string>>> StartsWith(string thumbprintStart)
        {
            return _certsByUser.Where(x => x.Key.StartsWith(thumbprintStart.ToUpperInvariant())).ToArray<KeyValuePair<string, string>>();
        }

        [HttpGet("unregister/{userName}")]
        public async Task UnregisterAsync(string userName)
        {
            var auth = ParseHeaders();
            if (auth.name == userName)
            {
                await _storageProvider.DeleteUserFolder(userName);
                _cache.Remove("certsByUser");
            }
            else
            {
                throw new SecurityException("Auth headers not found");
            }
        }

        //Chapu Authentication
        private (string name, Guid key) ParseHeaders()
        {
            if (!_httpContext.HttpContext.Request.Headers.ContainsKey("ApiKey"))
            {
                throw new SecurityException("Header not found");
            }

            var authHeader = _httpContext.HttpContext.Request.Headers["ApiKey"].
                        ToString().Split('#', StringSplitOptions.RemoveEmptyEntries);

            if (authHeader.Length < 2)
            {
                throw new SecurityException("Invalid Header");
            }

            var userName = authHeader[0];
            if (!Guid.TryParse(authHeader[1], out Guid key))
            {
                throw new SecurityException("Invalid Guid");
            }
            return (userName, key);
        }

        async Task LoadCertsFromBlobs()
        {
            if (_cache.TryGetValue("certsByUser", out CertsDictionary certs))
            {
                _certsByUser = certs;
            }
            else
            {
                _certsByUser = new CertsDictionary();
                var users = await _storageProvider.ListBlobsAsync();
                foreach (var user in users.Results)
                {
                    var userDir = user as CloudBlobDirectory;
                    var userName = userDir.Prefix.Replace("/", "");
                    var userFiles = await userDir.ListBlobsSegmentedAsync(null); //TODO: handle pagination
                    foreach (var uf in userFiles.Results)
                    {
                        var fileName = uf.StorageUri.PrimaryUri.Segments.Last();
                        if (fileName.EndsWith(".cer"))
                        {
                            var thumbprint = fileName.Replace(".cer", "");
                            _certsByUser.Add(thumbprint.ToUpperInvariant(), userName);
                        }
                    }
                }
                _cache.Set("certsByUser", _certsByUser);
            }
        }
    }
}
