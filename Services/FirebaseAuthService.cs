using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace FinPulse.Windows.Services;

/// <summary>
/// Production-ready Firebase Authentication service for FinPulse Windows.
/// Authenticates Google identities against the canonical Firebase project,
/// manages token lifecycles with single-flight concurrency locking, and
/// integrates with Windows secure credential storage.
/// </summary>
public class FirebaseAuthService : IAuthService
{
    private readonly HttpClient _httpClient;
    private readonly ISecureCredentialStorage _secureStorage;
    private readonly IGoogleOAuthHandler _googleOAuthHandler;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    private UserSession? _currentUser;

    public UserSession? CurrentUser => _currentUser;
    public bool IsLoggedIn => _currentUser != null && _currentUser.IsAuthenticated;

    public event EventHandler<UserSession?>? AuthStateChanged;

    public FirebaseAuthService(
        HttpClient? httpClient = null,
        ISecureCredentialStorage? secureStorage = null,
        IGoogleOAuthHandler? googleOAuthHandler = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        _secureStorage = secureStorage ?? new WindowsSecureCredentialStorage();
        _googleOAuthHandler = googleOAuthHandler ?? new GoogleOAuthHandler(_httpClient);
    }

    public async Task InitializeAsync()
    {
        try
        {
            var session = await _secureStorage.LoadSessionAsync();
            if (session != null && !string.IsNullOrWhiteSpace(session.Uid) && session.Uid != "local_default_user")
            {
                _currentUser = session;
                // Proactively refresh token in background if approaching expiration
                _ = Task.Run(async () =>
                {
                    try { await GetValidTokenAsync(); } catch { }
                });
                AuthStateChanged?.Invoke(this, _currentUser);
                return;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[FirebaseAuth] Could not load persisted credentials: {ex.Message}");
        }

        _currentUser = null;
        AuthStateChanged?.Invoke(this, null);
    }

    public async Task<UserSession> SignInWithGoogleAsync(CancellationToken cancellationToken = default)
    {
        // 1. Authenticate with Google via Desktop Browser + PKCE Loopback
        var oauthResult = await _googleOAuthHandler.AuthenticateViaBrowserAsync(cancellationToken);

        if (!oauthResult.IsSuccess)
        {
            if (oauthResult.IsCancelled)
            {
                throw new OperationCanceledException(oauthResult.ErrorMessage ?? "Google Sign-In was cancelled.");
            }

            throw new InvalidOperationException(oauthResult.ErrorMessage ?? "Google Sign-In failed.");
        }

        if (string.IsNullOrWhiteSpace(oauthResult.IdToken))
        {
            throw new InvalidOperationException("No Google ID token received from authentication flow.");
        }

        // 2. Exchange Google ID token with Firebase Auth signInWithIdp
        return await SignInWithGoogleTokenAsync(oauthResult.IdToken);
    }

    public async Task<UserSession> SignInWithGoogleTokenAsync(string googleIdToken)
    {
        if (string.IsNullOrWhiteSpace(googleIdToken))
            throw new ArgumentException("Google ID token cannot be empty.", nameof(googleIdToken));

        string endpoint = $"{FirebaseConfig.BaseAuthUrl}:signInWithIdp?key={FirebaseConfig.ApiKey}";
        var payload = new
        {
            postBody = $"id_token={googleIdToken}&providerId=google.com",
            requestUri = "http://localhost",
            returnIdpCredential = true,
            returnSecureToken = true
        };

        var response = await PostJsonAsync(endpoint, payload);
        using var doc = JsonDocument.Parse(response);
        var root = doc.RootElement;

        // Extract canonical Firebase UID (localId)
        string localId = root.GetProperty("localId").GetString()
                         ?? throw new InvalidOperationException("Firebase signInWithIdp did not return a localId.");

        string idToken = root.GetProperty("idToken").GetString() ?? "";
        string? refreshToken = root.TryGetProperty("refreshToken", out var rtProp) ? rtProp.GetString() : null;
        string? email = root.TryGetProperty("email", out var emProp) ? emProp.GetString() : null;
        string? displayName = root.TryGetProperty("displayName", out var dnProp) ? dnProp.GetString() : null;
        string? photoUrl = root.TryGetProperty("photoUrl", out var puProp) ? puProp.GetString() : null;

        long expiresIn = 3600;
        if (root.TryGetProperty("expiresIn", out var expProp))
        {
            long.TryParse(expProp.GetString(), out expiresIn);
        }

        var session = new UserSession
        {
            Uid = localId,
            Email = email,
            DisplayName = !string.IsNullOrWhiteSpace(displayName) ? displayName : (email?.Split('@')[0] ?? "Google User"),
            PhotoUrl = photoUrl,
            IdToken = idToken,
            RefreshToken = refreshToken,
            ExpiresAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + expiresIn,
            ProviderId = "google.com",
            IsAnonymous = false
        };

        Debug.WriteLine($"[Auth] Google authentication succeeded. Firebase UID (localId): {localId}, Email: {email}, Provider: google.com");
        await SetSessionAsync(session);
        return session;
    }

    public async Task<UserSession> SignInWithGoogleAccountAsync(string email, string? displayName = null, string? photoUrl = null)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Google account email cannot be empty.", nameof(email));

        string cleanEmail = email.Trim().ToLowerInvariant();
        string name = !string.IsNullOrWhiteSpace(displayName)
            ? displayName.Trim()
            : char.ToUpper(cleanEmail[0]) + cleanEmail.Split('@')[0][1..];

        // Generate consistent deterministic UID for test/offline sessions
        using var sha = System.Security.Cryptography.SHA256.Create();
        byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes("google:" + cleanEmail));
        string hex = Convert.ToHexString(hash).ToLowerInvariant()[..24];
        string uid = $"google_{hex}";

        var session = new UserSession
        {
            Uid = uid,
            Email = cleanEmail,
            DisplayName = name,
            PhotoUrl = photoUrl,
            ProviderId = "google.com",
            IdToken = $"mock_google_id_token_{hex}",
            RefreshToken = $"mock_google_refresh_token_{hex}",
            ExpiresAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 86400 * 30, // 30 days
            IsAnonymous = false
        };

        await SetSessionAsync(session);
        return session;
    }

    public async Task<UserSession> SignInWithEmailPasswordAsync(string email, string password)
    {
        string endpoint = $"{FirebaseConfig.BaseAuthUrl}:signInWithPassword?key={FirebaseConfig.ApiKey}";
        var payload = new
        {
            email,
            password,
            returnSecureToken = true
        };

        var response = await PostJsonAsync(endpoint, payload);
        using var doc = JsonDocument.Parse(response);
        var root = doc.RootElement;

        long expiresIn = 3600;
        if (root.TryGetProperty("expiresIn", out var expProp))
        {
            long.TryParse(expProp.GetString(), out expiresIn);
        }

        var session = new UserSession
        {
            Uid = root.GetProperty("localId").GetString() ?? Guid.NewGuid().ToString(),
            Email = root.GetProperty("email").GetString(),
            DisplayName = root.TryGetProperty("displayName", out var dn) ? dn.GetString() : email.Split('@')[0],
            IdToken = root.GetProperty("idToken").GetString() ?? "",
            RefreshToken = root.GetProperty("refreshToken").GetString(),
            ExpiresAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + expiresIn,
            ProviderId = "password",
            IsAnonymous = false
        };

        await SetSessionAsync(session);
        return session;
    }

    public async Task<UserSession> SignUpWithEmailPasswordAsync(string email, string password)
    {
        string endpoint = $"{FirebaseConfig.BaseAuthUrl}:signUp?key={FirebaseConfig.ApiKey}";
        var payload = new
        {
            email,
            password,
            returnSecureToken = true
        };

        var response = await PostJsonAsync(endpoint, payload);
        using var doc = JsonDocument.Parse(response);
        var root = doc.RootElement;

        long expiresIn = 3600;
        if (root.TryGetProperty("expiresIn", out var expProp))
        {
            long.TryParse(expProp.GetString(), out expiresIn);
        }

        var session = new UserSession
        {
            Uid = root.GetProperty("localId").GetString() ?? Guid.NewGuid().ToString(),
            Email = root.GetProperty("email").GetString(),
            DisplayName = email.Split('@')[0],
            IdToken = root.GetProperty("idToken").GetString() ?? "",
            RefreshToken = root.GetProperty("refreshToken").GetString(),
            ExpiresAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + expiresIn,
            ProviderId = "password",
            IsAnonymous = false
        };

        await SetSessionAsync(session);
        return session;
    }

    public async Task<UserSession> SignInAnonymouslyAsync()
    {
        string endpoint = $"{FirebaseConfig.BaseAuthUrl}:signUp?key={FirebaseConfig.ApiKey}";
        var payload = new { returnSecureToken = true };

        var response = await PostJsonAsync(endpoint, payload);
        using var doc = JsonDocument.Parse(response);
        var root = doc.RootElement;

        long expiresIn = 3600;
        if (root.TryGetProperty("expiresIn", out var expProp))
        {
            long.TryParse(expProp.GetString(), out expiresIn);
        }

        var session = new UserSession
        {
            Uid = root.GetProperty("localId").GetString() ?? Guid.NewGuid().ToString(),
            DisplayName = "Guest User",
            IdToken = root.GetProperty("idToken").GetString() ?? "",
            RefreshToken = root.GetProperty("refreshToken").GetString(),
            ExpiresAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + expiresIn,
            ProviderId = "anonymous",
            IsAnonymous = true
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
            ProviderId = "demo",
            IsAnonymous = false
        };

        await SetSessionAsync(session);
        return session;
    }

    public async Task SignOutAsync()
    {
        _currentUser = null;
        try
        {
            await _secureStorage.ClearSessionAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[FirebaseAuth] Error clearing credentials: {ex.Message}");
        }

        AuthStateChanged?.Invoke(this, null);
    }

    public async Task<string?> GetValidTokenAsync(CancellationToken cancellationToken = default)
    {
        if (_currentUser == null) return null;

        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        // If token has more than 2 minutes of lifetime remaining, return current token immediately
        if (now < _currentUser.ExpiresAt - 120 && !string.IsNullOrWhiteSpace(_currentUser.IdToken))
        {
            return _currentUser.IdToken;
        }

        if (string.IsNullOrWhiteSpace(_currentUser.RefreshToken))
        {
            return _currentUser.IdToken;
        }

        // Single-flight locking to prevent 10 concurrent requests from triggering 10 simultaneous refresh calls
        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            // Double-check inside lock
            now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (now < _currentUser.ExpiresAt - 120 && !string.IsNullOrWhiteSpace(_currentUser.IdToken))
            {
                return _currentUser.IdToken;
            }

            string url = $"{FirebaseConfig.SecureTokenUrl}?key={FirebaseConfig.ApiKey}";
            var postData = new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = _currentUser.RefreshToken
            };

            using var content = new FormUrlEncodedContent(postData);
            using var response = await _httpClient.PostAsync(url, content, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                string resStr = await response.Content.ReadAsStringAsync(cancellationToken);
                using var doc = JsonDocument.Parse(resStr);
                var root = doc.RootElement;

                _currentUser.IdToken = root.GetProperty("id_token").GetString() ?? _currentUser.IdToken;
                if (root.TryGetProperty("refresh_token", out var rt))
                {
                    _currentUser.RefreshToken = rt.GetString() ?? _currentUser.RefreshToken;
                }
                string expiresInStr = root.GetProperty("expires_in").GetString() ?? "3600";
                long.TryParse(expiresInStr, out long expiresIn);
                _currentUser.ExpiresAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + expiresIn;

                await _secureStorage.SaveSessionAsync(_currentUser);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[FirebaseAuth] Token refresh warning: {ex.Message}");
            // Return existing token as fallback if network is temporarily unreachable
        }
        finally
        {
            _refreshLock.Release();
        }

        return _currentUser.IdToken;
    }

    private async Task SetSessionAsync(UserSession session)
    {
        _currentUser = session;
        try
        {
            await _secureStorage.SaveSessionAsync(session);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[FirebaseAuth] Could not persist session: {ex.Message}");
        }

        AuthStateChanged?.Invoke(this, _currentUser);
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
