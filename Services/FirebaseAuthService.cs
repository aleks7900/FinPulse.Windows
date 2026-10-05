using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace FinPulse.Windows.Services;

public class FirebaseAuthService : IAuthService
{
    private const string ApiKey = "AIzaSyAptMbFVj25my_TsUrvPn_6DvvSJJQF_-o";
    private const string BaseAuthUrl = "https://identitytoolkit.googleapis.com/v1/accounts";
    private const string SecureTokenUrl = "https://securetoken.googleapis.com/v1/token";

    private readonly HttpClient _httpClient;
    private readonly string _sessionFilePath;
    private UserSession? _currentUser;

    public UserSession? CurrentUser => _currentUser;
    public bool IsLoggedIn => _currentUser != null && _currentUser.IsAuthenticated;

    public event EventHandler<UserSession?>? AuthStateChanged;

    public FirebaseAuthService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();

        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string appDir = Path.Combine(localAppData, "FinPulseCompanion");
        Directory.CreateDirectory(appDir);
        _sessionFilePath = Path.Combine(appDir, "session.json");
    }

    public async Task InitializeAsync()
    {
        try
        {
            if (File.Exists(_sessionFilePath))
            {
                string json = await File.ReadAllTextAsync(_sessionFilePath);
                var session = JsonSerializer.Deserialize<UserSession>(json);
                if (session != null && !string.IsNullOrWhiteSpace(session.Uid))
                {
                    _currentUser = session;
                    // Proactively refresh token if expired
                    _ = RefreshTokenIfNeededAsync();
                    AuthStateChanged?.Invoke(this, _currentUser);
                    return;
                }
            }
        }
        catch (Exception)
        {
            // Ignore corrupted session file
        }

        // If no prior session, initialize default local session for seamless initial experience
        _currentUser = new UserSession
        {
            Uid = "local_default_user",
            Email = "local@finpulse.app",
            DisplayName = "Local User",
            IsAnonymous = true
        };
        AuthStateChanged?.Invoke(this, _currentUser);
    }

    public async Task<UserSession> SignInWithEmailPasswordAsync(string email, string password)
    {
        string endpoint = $"{BaseAuthUrl}:signInWithPassword?key={ApiKey}";
        var payload = new
        {
            email,
            password,
            returnSecureToken = true
        };

        var response = await PostJsonAsync(endpoint, payload);
        using var doc = JsonDocument.Parse(response);
        var root = doc.RootElement;

        var session = new UserSession
        {
            Uid = root.GetProperty("localId").GetString() ?? Guid.NewGuid().ToString(),
            Email = root.GetProperty("email").GetString(),
            DisplayName = root.TryGetProperty("displayName", out var dn) ? dn.GetString() : email.Split('@')[0],
            IdToken = root.GetProperty("idToken").GetString() ?? "",
            RefreshToken = root.GetProperty("refreshToken").GetString(),
            ExpiresAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + long.Parse(root.GetProperty("expiresIn").GetString() ?? "3600"),
            IsAnonymous = false
        };

        await SetSessionAsync(session);
        return session;
    }

    public async Task<UserSession> SignUpWithEmailPasswordAsync(string email, string password)
    {
        string endpoint = $"{BaseAuthUrl}:signUp?key={ApiKey}";
        var payload = new
        {
            email,
            password,
            returnSecureToken = true
        };

        var response = await PostJsonAsync(endpoint, payload);
        using var doc = JsonDocument.Parse(response);
        var root = doc.RootElement;

        var session = new UserSession
        {
            Uid = root.GetProperty("localId").GetString() ?? Guid.NewGuid().ToString(),
            Email = root.GetProperty("email").GetString(),
            DisplayName = email.Split('@')[0],
            IdToken = root.GetProperty("idToken").GetString() ?? "",
            RefreshToken = root.GetProperty("refreshToken").GetString(),
            ExpiresAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + long.Parse(root.GetProperty("expiresIn").GetString() ?? "3600"),
            IsAnonymous = false
        };

        await SetSessionAsync(session);
        return session;
    }

    public async Task<UserSession> SignInAnonymouslyAsync()
    {
        string endpoint = $"{BaseAuthUrl}:signUp?key={ApiKey}";
        var payload = new { returnSecureToken = true };

        var response = await PostJsonAsync(endpoint, payload);
        using var doc = JsonDocument.Parse(response);
        var root = doc.RootElement;

        var session = new UserSession
        {
            Uid = root.GetProperty("localId").GetString() ?? Guid.NewGuid().ToString(),
            DisplayName = "Guest User",
            IdToken = root.GetProperty("idToken").GetString() ?? "",
            RefreshToken = root.GetProperty("refreshToken").GetString(),
            ExpiresAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + long.Parse(root.GetProperty("expiresIn").GetString() ?? "3600"),
            IsAnonymous = true
        };

        await SetSessionAsync(session);
        return session;
    }

    public async Task<UserSession> SignInWithGoogleTokenAsync(string idToken)
    {
        string endpoint = $"{BaseAuthUrl}:signInWithIdp?key={ApiKey}";
        var payload = new
        {
            postBody = $"id_token={idToken}&providerId=google.com",
            requestUri = "http://localhost",
            returnIdpCredential = true,
            returnSecureToken = true
        };

        var response = await PostJsonAsync(endpoint, payload);
        using var doc = JsonDocument.Parse(response);
        var root = doc.RootElement;

        var session = new UserSession
        {
            Uid = root.GetProperty("localId").GetString() ?? Guid.NewGuid().ToString(),
            Email = root.TryGetProperty("email", out var em) ? em.GetString() : null,
            DisplayName = root.TryGetProperty("displayName", out var dn) ? dn.GetString() : "Google User",
            PhotoUrl = root.TryGetProperty("photoUrl", out var pu) ? pu.GetString() : null,
            IdToken = root.GetProperty("idToken").GetString() ?? "",
            RefreshToken = root.GetProperty("refreshToken").GetString(),
            ExpiresAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + long.Parse(root.GetProperty("expiresIn").GetString() ?? "3600"),
            IsAnonymous = false
        };

        await SetSessionAsync(session);
        return session;
    }

    public async Task<UserSession> UseDemoAccountAsync()
    {
        var session = new UserSession
        {
            Uid = "demo_account_finpulse",
            Email = "demo@finpulse.app",
            DisplayName = "Alex (FinPulse Demo)",
            IsAnonymous = false
        };

        await SetSessionAsync(session);
        return session;
    }

    public async Task SignOutAsync()
    {
        _currentUser = null;
        if (File.Exists(_sessionFilePath))
        {
            try { File.Delete(_sessionFilePath); } catch { }
        }
        AuthStateChanged?.Invoke(this, null);
        await Task.CompletedTask;
    }

    public async Task<string?> GetValidTokenAsync()
    {
        if (_currentUser == null) return null;

        if (string.IsNullOrWhiteSpace(_currentUser.RefreshToken))
            return _currentUser.IdToken;

        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (now >= _currentUser.ExpiresAt - 120) // Refresh 2 minutes before expiry
        {
            await RefreshTokenIfNeededAsync();
        }

        return _currentUser.IdToken;
    }

    private async Task RefreshTokenIfNeededAsync()
    {
        if (_currentUser == null || string.IsNullOrWhiteSpace(_currentUser.RefreshToken))
            return;

        try
        {
            string url = $"{SecureTokenUrl}?key={ApiKey}";
            var payload = new
            {
                grant_type = "refresh_token",
                refresh_token = _currentUser.RefreshToken
            };

            var jsonContent = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(url, jsonContent);
            if (response.IsSuccessStatusCode)
            {
                string resStr = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(resStr);
                var root = doc.RootElement;

                _currentUser.IdToken = root.GetProperty("id_token").GetString() ?? _currentUser.IdToken;
                _currentUser.RefreshToken = root.GetProperty("refresh_token").GetString() ?? _currentUser.RefreshToken;
                _currentUser.ExpiresAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + long.Parse(root.GetProperty("expires_in").GetString() ?? "3600");

                await SaveSessionToFileAsync();
            }
        }
        catch
        {
            // If offline, continue using current token
        }
    }

    private async Task SetSessionAsync(UserSession session)
    {
        _currentUser = session;
        await SaveSessionToFileAsync();
        AuthStateChanged?.Invoke(this, _currentUser);
    }

    private async Task SaveSessionToFileAsync()
    {
        if (_currentUser == null) return;
        try
        {
            string json = JsonSerializer.Serialize(_currentUser, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_sessionFilePath, json);
        }
        catch { }
    }

    private async Task<string> PostJsonAsync(string url, object body)
    {
        string json = JsonSerializer.Serialize(body);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync(url, content);
        string responseString = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            string errorMsg = "Authentication failed";
            try
            {
                using var errDoc = JsonDocument.Parse(responseString);
                if (errDoc.RootElement.TryGetProperty("error", out var err) &&
                    err.TryGetProperty("message", out var msg))
                {
                    errorMsg = msg.GetString() ?? errorMsg;
                }
            }
            catch { }
            throw new InvalidOperationException(errorMsg);
        }

        return responseString;
    }
}
