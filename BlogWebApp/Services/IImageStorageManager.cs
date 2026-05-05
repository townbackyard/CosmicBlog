using System.Threading;
using System.Threading.Tasks;
using Azure;
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
}
