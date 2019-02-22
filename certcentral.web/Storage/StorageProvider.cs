using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace certcentral.web.Storage
{
    public static class StreamExtensions
    {
        public static byte[] ReadAllBytes(this Stream s)
        {
            using (var ms = new MemoryStream())
            {
                s.CopyTo(ms);
                return ms.ToArray();
            }
        }
    }
    public class StorageProvider
    {
        private readonly StorageOptions _options;

        private Lazy<CloudBlobContainer> _usersContainer;
        private CloudBlobContainer UsersContainer => _usersContainer.Value;

        public StorageProvider(IOptions<StorageOptions> optionsAccessor)
        {
            _options = optionsAccessor.Value;
            //TODO: REVIEW THIS INITIALIZATION
            _usersContainer = new Lazy<CloudBlobContainer>(
                () => { return GetOrCreateContainerAsync("users").Result; }, 
                true);
        }

        public async Task<string> GetBlobAsync(string path)
        {
            var blob = UsersContainer.GetBlockBlobReference(path);
            return await blob.DownloadTextAsync();
        }

        public async Task<byte[]> ReadBlobAsync(string path)
        {
            var blob = UsersContainer.GetBlockBlobReference(path);
            using (var s = await blob.OpenReadAsync())
            {
                return s.ReadAllBytes();
            }
        }

        public async Task<bool> BlobExistsAsync(string path)
        {
            var blob = UsersContainer.GetBlobReference(path);
            return await blob.ExistsAsync();
        }

        public async Task CreateOrUpdateAsync(string content, string path)
        {
            var blob = UsersContainer.GetBlockBlobReference(path);
            await blob.UploadTextAsync(content);
        }

        public async Task CreateOrUpdateAsync(Stream content, string path)
        {
            var blob = UsersContainer.GetBlockBlobReference(path);
            await blob.UploadFromStreamAsync(content);
        }

        public async Task DeleteAsync(string path)
        {
            //TODO: ENCAPSULATE BLOB REFERENCE
            var blob = UsersContainer.GetBlockBlobReference(path);
            await blob.DeleteIfExistsAsync();
        }

        public async Task<BlobResultSegment> ListBlobsAsync()
        {
            //TODO: Handle pagination
            return await UsersContainer.ListBlobsSegmentedAsync(null);
        }

        private async Task<CloudBlobContainer> GetOrCreateContainerAsync(string name)
        {
            if (CloudStorageAccount.TryParse(_options.ConnectionString, out CloudStorageAccount storageAccount))
            {
                CloudBlobClient cloudBlobClient = storageAccount.CreateCloudBlobClient();
                //var usersContainer = cloudBlobClient.GetRootContainerReference();
                var usersContainer = cloudBlobClient.GetContainerReference(name);
                await usersContainer.CreateIfNotExistsAsync();

                return usersContainer;
            }


            throw new NotImplementedException();
        }
    }
}
