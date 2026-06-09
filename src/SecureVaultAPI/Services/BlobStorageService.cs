using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using SecureVaultAPI.Models;

namespace SecureVaultAPI.Services;

public class BlobStorageService : IBlobStorageService
{
    private readonly BlobContainerClient _container;

    public BlobStorageService(IKeyVaultService keyVault, IConfiguration config)
    {
        var connectionString = keyVault.GetSecretAsync("StorageConnectionString").GetAwaiter().GetResult();
        var containerName = config["Azure:StorageContainerName"] ?? "documents";
        _container = new BlobContainerClient(connectionString, containerName);
    }

    public async Task<string> UploadAsync(string userId, IFormFile file)
    {
        var documentId = Guid.NewGuid().ToString();
        var blobName = $"{userId}/{documentId}/{file.FileName}";
        var blobClient = _container.GetBlobClient(blobName);

        var metadata = new Dictionary<string, string>
        {
            { "userId", userId },
            { "documentId", documentId },
            { "originalFileName", file.FileName },
            { "uploadedAt", DateTimeOffset.UtcNow.ToString("O") }
        };

        using var stream = file.OpenReadStream();
        await blobClient.UploadAsync(stream, new BlobUploadOptions
        {
            Metadata = metadata,
            HttpHeaders = new BlobHttpHeaders
            {
                ContentType = file.ContentType
            }
        });

        return documentId;
    }

    public async Task<IEnumerable<DocumentMetadata>> ListAsync(string userId)
    {
        var results = new List<DocumentMetadata>();
        var prefix = $"{userId}/";

        await foreach (var blob in _container.GetBlobsAsync(
     traits: BlobTraits.Metadata,
     states: BlobStates.None,
     prefix: prefix,
     cancellationToken: CancellationToken.None))
        {
            results.Add(new DocumentMetadata
            {
                Id = blob.Metadata.TryGetValue("documentId", out var id) ? id : blob.Name,
                FileName = blob.Metadata.TryGetValue("originalFileName", out var name) ? name : blob.Name,
                SizeBytes = blob.Properties.ContentLength ?? 0,
                UploadedAt = blob.Properties.CreatedOn ?? DateTimeOffset.MinValue,
                UploadedBy = userId
            });
        }

        return results;
    }

    public async Task<Stream> DownloadAsync(string userId, string documentId)
    {
        var prefix = $"{userId}/{documentId}/";

        await foreach (var blob in _container.GetBlobsAsync(
     traits: BlobTraits.None,
     states: BlobStates.None,
     prefix: prefix,
     cancellationToken: CancellationToken.None))
        {
            var blobClient = _container.GetBlobClient(blob.Name);
            var download = await blobClient.DownloadStreamingAsync();
            return download.Value.Content;
        }

        throw new FileNotFoundException($"Document {documentId} not found for user {userId}");
    }
}