using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureVaultAPI.Services;
using System.Security.Claims;

namespace SecureVaultAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DocumentsController : ControllerBase
{
    private readonly IBlobStorageService _storage;
    private readonly ILogger<DocumentsController> _logger;

    public DocumentsController(IBlobStorageService storage, ILogger<DocumentsController> logger)
    {
        _storage = storage;
        _logger = logger;
    }

    [HttpPost("upload")]
    public async Task<IActionResult> Upload(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "No file provided" });

        if (file.Length > 10 * 1024 * 1024)
            return BadRequest(new { message = "File exceeds 10MB limit" });

        var allowedTypes = new[] { "application/pdf", "image/jpeg", "image/png", "text/plain" };
        if (!allowedTypes.Contains(file.ContentType))
            return BadRequest(new { message = "File type not allowed" });

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("User ID not found in token");

        _logger.LogInformation("Upload initiated by {UserId} for file {FileName}", userId, file.FileName);

        var documentId = await _storage.UploadAsync(userId, file);

        return Ok(new { documentId, message = "Upload successful" });
    }

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("User ID not found in token");

        var documents = await _storage.ListAsync(userId);
        return Ok(documents);
    }

    [HttpGet("{documentId}")]
    public async Task<IActionResult> Download(string documentId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("User ID not found in token");

        try
        {
            var stream = await _storage.DownloadAsync(userId, documentId);
            return File(stream, "application/octet-stream");
        }
        catch (FileNotFoundException)
        {
            return NotFound(new { message = "Document not found" });
        }
    }
}