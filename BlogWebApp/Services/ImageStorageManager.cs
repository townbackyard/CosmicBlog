using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace BlogWebApp.Services
{
    public interface IImageStorageManager
    {
        Task<bool> BlobExists(string containerName, string blobName);

        Task<byte[]> GetBlobAsByteArray(string containerName, string blobName);

        Task<Response<BlobDownloadStreamingResult>> GetBlobAsStream(string containerName, string blobName, CancellationToken ct);

        Task UploadBlob(string containerName, string blobName, string contentType, byte[] buffer);
    }

    public class ImageStorageManager : IImageStorageManager
    {
        private string StorageBlobConnectionString { get; }

        public ImageStorageManager(string storageBlobConnectionString)
        {
            StorageBlobConnectionString = storageBlobConnectionString;
        }


        public async Task<bool> BlobExists(string containerName, string blobName)
        {
            var containerClient = await GetContainerClient(containerName);

            // trim off any leading "/". blobNames with paths need to be in form like "directory/blob_name.png"
            blobName = blobName.TrimStart('/');

            var blobClient = containerClient.GetBlobClient(blobName);
            return await blobClient.ExistsAsync();
        }


        public async Task<byte[]> GetBlobAsByteArray(string containerName, string blobName)
        {
            var containerClient = await GetContainerClient(containerName);

            // trim off any leading "/". blobNames with paths need to be in form like "directory/blob_name.png"
            blobName = blobName.TrimStart('/');

            var blobClient = containerClient.GetBlobClient(blobName);

            if (!await blobClient.ExistsAsync())
            {
                throw new InvalidOperationException($"The block blob {blobName} does not exist.");
            }

            using var ms = new MemoryStream();
            await blobClient.DownloadToAsync(ms);
            return ms.ToArray();
        }

        public async Task<Response<BlobDownloadStreamingResult>> GetBlobAsStream(string containerName, string blobName, CancellationToken ct)
        {
            var containerClient = await GetContainerClient(containerName);

            // trim off any leading "/". blobNames with paths need to be in form like "directory/blob_name.png"
            blobName = blobName.TrimStart('/');

            var blobClient = containerClient.GetBlobClient(blobName);

            if (!await blobClient.ExistsAsync())
            {
                throw new InvalidOperationException($"The block blob {blobName} does not exist.");
            }

            var blob = await blobClient.DownloadStreamingAsync(cancellationToken: ct);
            return blob;
        }

        public async Task UploadBlob(string containerName, string blobName, string contentType, byte[] buffer)
        {
            var containerClient = await GetContainerClient(containerName);

            // trim off any leading "/". blobNames with paths need to be in form like "directory/blob_name.png"
            blobName = blobName.TrimStart('/');

            var blobClient = containerClient.GetBlobClient(blobName);
            var binaryData = new BinaryData(buffer);

            await blobClient.UploadAsync(binaryData, overwrite: true);
            await blobClient.SetHttpHeadersAsync(new BlobHttpHeaders
            {
                ContentType = contentType
            });
        }


        private static readonly ConcurrentDictionary<string, object> ContainerNameList = new();

        private async Task<BlobContainerClient> GetContainerClient(string containerName)
        {
            var containerClient = new BlobContainerClient(StorageBlobConnectionString, containerName);

            // check to see if we already created the ContainerName
            if (!ContainerNameList.ContainsKey(containerName))
            {
                // create a private blob container
                await containerClient.CreateIfNotExistsAsync();
                ContainerNameList.TryAdd(containerName, new object());
            }

            return containerClient;
        }
    }
}
