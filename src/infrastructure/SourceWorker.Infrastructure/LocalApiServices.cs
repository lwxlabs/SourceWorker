using Microsoft.Extensions.Options;
using SourceWorker.Application;
using SourceWorker.Domain;
using SourceWorker.Contracts;
using System.Diagnostics;
using System.Security.Cryptography;

namespace SourceWorker.Infrastructure;

public sealed class LocalApiRuntimeService : ILocalApiRuntimeService
{
    private readonly LocalApiRuntimeState _state;

    public LocalApiRuntimeService(IOptions<LocalApiRuntimeOptions> options, ILocalApiTokenService tokenService)
    {
        var opt = options.Value;
        var startupUtc = DateTimeOffset.UtcNow;
        var processId = Process.GetCurrentProcess().Id;
        var environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";

        var selectedHost = opt.Host ?? "localhost";
        var selectedPort = opt.Port ?? (opt.UseRandomPort ? 0 : 5000); // Default fallback

        _state = new LocalApiRuntimeState(
            selectedHost,
            selectedPort,
            $"http://{selectedHost}:{selectedPort}",
            opt.RequireToken,
            tokenService.GetTokenCreatedUtc(),
            opt.AllowedOrigins ?? Array.Empty<string>(),
            opt.AllowedOrigins?.FirstOrDefault(),
            processId,
            startupUtc,
            environmentName
        );
    }

    public LocalApiRuntimeState GetState() => _state;
}

public sealed class LocalApiTokenService : ILocalApiTokenService
{
    private readonly string _tokenFilePath;
    private string? _cachedToken;
    private DateTimeOffset? _tokenCreatedUtc;

    public LocalApiTokenService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dir = Path.Combine(appData, "SourceWorker");
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        _tokenFilePath = Path.Combine(dir, "api-token.txt");
        LoadToken();
        
        if (string.IsNullOrEmpty(_cachedToken))
        {
            GenerateAndSaveToken();
        }
    }

    private void LoadToken()
    {
        if (File.Exists(_tokenFilePath))
        {
            _cachedToken = File.ReadAllText(_tokenFilePath).Trim();
            _tokenCreatedUtc = new DateTimeOffset(File.GetCreationTimeUtc(_tokenFilePath), TimeSpan.Zero);
        }
    }

    public string? GetCurrentToken() => _cachedToken;

    public string GenerateAndSaveToken()
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        File.WriteAllText(_tokenFilePath, token);
        _cachedToken = token;
        _tokenCreatedUtc = DateTimeOffset.UtcNow;
        return token;
    }

    public bool ValidateToken(string token)
    {
        if (string.IsNullOrEmpty(_cachedToken)) return false;
        return _cachedToken == token;
    }

    public DateTimeOffset? GetTokenCreatedUtc() => _tokenCreatedUtc;
}

public sealed class LocalApiOriginService : ILocalApiOriginService
{
    private readonly string[] _allowedOrigins;

    public LocalApiOriginService(IOptions<LocalApiRuntimeOptions> options)
    {
        var opt = options.Value;
        var list = new List<string>();
        if (opt.AllowedOrigins != null) list.AddRange(opt.AllowedOrigins);
        
        // Always allow localhost for development if not already present
        if (!list.Contains("http://localhost:5001")) list.Add("http://localhost:5001");
        if (!list.Contains("https://localhost:5001")) list.Add("https://localhost:5001");
        
        _allowedOrigins = list.ToArray();
    }

    public bool IsOriginAllowed(string? origin)
    {
        if (string.IsNullOrEmpty(origin)) return false;
        if (_allowedOrigins.Contains("*")) return true;
        return _allowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase);
    }

    public string[] GetAllowedOrigins() => _allowedOrigins;
}

public sealed class AppVersionService : IAppVersionService
{
    public VersionInfoDto GetVersion()
    {
        return new VersionInfoDto(
            "SourceWorker",
            "1.8.0",
            "1.8",
            "1.8",
            1, // Storage schema version
            5, // Context format version
            "Release"
        );
    }
}
