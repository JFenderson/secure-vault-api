using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using SecureVaultAPI;
using SecureVaultAPI.Services;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

Microsoft.IdentityModel.Logging.IdentityModelEventSource.ShowPII = true;


builder.Services.AddControllers();
builder.Services.AddSingleton<IKeyVaultService, KeyVaultService>();
builder.Services.AddScoped<IBlobStorageService, BlobStorageService>();

// Pull JWT secret from Key Vault at startup
var vaultUri = builder.Configuration["KeyVault:VaultUri"]
    ?? throw new InvalidOperationException("KeyVault:VaultUri not configured");

var secretClient = new SecretClient(new Uri(vaultUri), new DefaultAzureCredential());
var jwtSecret = secretClient.GetSecret("JwtSecret").Value.Value;

JwtSecretHolder.Secret = jwtSecret;

//builder.Configuration["Jwt:Secret"] = jwtSecret;

//Console.WriteLine($"JWT secret loaded, length: {jwtSecret.Length}, first 4 chars: {jwtSecret[..4]}");
Console.WriteLine($"JWT secret loaded, length: {jwtSecret.Length}, first 4 chars: {jwtSecret[..4]}");
Console.WriteLine($"Holder secret length: {JwtSecretHolder.Secret.Length}");


builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            //IssuerSigningKey = new SymmetricSecurityKey(
            //    Encoding.UTF8.GetBytes(jwtSecret))
            IssuerSigningKey = new SymmetricSecurityKey(
    Encoding.UTF8.GetBytes(JwtSecretHolder.Secret))
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var authHeader = context.Request.Headers["Authorization"].FirstOrDefault() ?? string.Empty;
                Console.WriteLine($"Auth header length: {authHeader.Length}");

                if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    var extracted = authHeader.Substring(7).Trim();
                    context.Token = extracted;
                    Console.WriteLine($"Token set, length: {context.Token.Length}, starts: {context.Token[..10]}");
                }

                return Task.CompletedTask;
            },
            OnAuthenticationFailed = context =>
            {
                Console.WriteLine($"JWT auth failed: {context.Exception.Message}");
                return Task.CompletedTask;
            },
            OnChallenge = context =>
            {
                Console.WriteLine($"JWT challenge: {context.Error}, {context.ErrorDescription}");
                return Task.CompletedTask;
            }
        };


    });

builder.Services.AddAuthorization();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));


app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();