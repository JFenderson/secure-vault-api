using SecureVaultAPI.Models;

namespace SecureVaultAPI.Services;

public interface IBlobStorageService
{
    Task<string> UploadAsync(string userId, IFormFile file);
    Task<IEnumerable<DocumentMetadata>> ListAsync(string userId);
    Task<Stream> DownloadAsync(string userId, string documentId);
}