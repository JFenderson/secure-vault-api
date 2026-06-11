using Microsoft.AspNetCore.Mvc;
using SecureVaultAPI.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace SecureVaultAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DocumentsController : ControllerBase
{
    private readonly IBlobStorageService _storage;
    private readonly ILogger<DocumentsController> _logger;

    public DocumentsController(IBlobStorageService storage, ILogger<DocumentsController> logger)
    {
        _storage = storage;
        _logger = logger;
    }

    private ClaimsPrincipal? ValidateToken(string? authHeader)
    {
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            return null;

        var token = authHeader.Substring(7).Trim();
        var secret = JwtSecretHolder.Secret;

        Console.WriteLine($"Manual validate - token length: {token.Length}, secret length: {secret.Length}");

        try
        {
            var handler = new JwtSecurityTokenHandler();
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));

            var principal = handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidateIssuer = true,
                ValidIssuer = "SecureVaultAPI",
                ValidateAudience = true,
                ValidAudience = "SecureVaultAPIUsers",
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            }, out _);

            Console.WriteLine($"Manual validate - success, user: {principal.Identity?.Name}");
            return principal;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Manual validate failed: {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    [HttpPost("upload")]
    public async Task<IActionResult> Upload(IFormFile file)
    {
        var principal = ValidateToken(Request.Headers["Authorization"].FirstOrDefault());
        if (principal == null) return Unauthorized();

        if (file == null || file.Length == 0)
            return BadRequest(new { message = "No file provided" });

        if (file.Length > 10 * 1024 * 1024)
            return BadRequest(new { message = "File exceeds 10MB limit" });

        var allowedTypes = new[] { "application/pdf", "image/jpeg", "image/png", "text/plain" };
        if (!allowedTypes.Contains(file.ContentType))
            return BadRequest(new { message = "File type not allowed" });

        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("User ID not found in token");

        _logger.LogInformation("Upload initiated by {UserId} for file {FileName}", userId, file.FileName);
        var documentId = await _storage.UploadAsync(userId, file);
        return Ok(new { documentId, message = "Upload successful" });
    }

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var principal = ValidateToken(Request.Headers["Authorization"].FirstOrDefault());
        if (principal == null) return Unauthorized();

        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("User ID not found in token");

        var documents = await _storage.ListAsync(userId);
        return Ok(documents);
    }

    [HttpGet("{documentId}")]
    public async Task<IActionResult> Download(string documentId)
    {
        var principal = ValidateToken(Request.Headers["Authorization"].FirstOrDefault());
        if (principal == null) return Unauthorized();

        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier)
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