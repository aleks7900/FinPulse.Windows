using System;
using System.Threading;
using System.Threading.Tasks;

namespace FinPulse.Windows.Services;

public class UserSession
{
    public string Uid { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? DisplayName { get; set; }
    public string? PhotoUrl { get; set; }
    public string IdToken { get; set; } = string.Empty;
    public string? RefreshToken { get; set; }
    public long ExpiresAt { get; set; }
    public bool IsAnonymous { get; set; }

    public string? ProviderId { get; set; } = "firebase";

    public bool IsGoogleUser => ProviderId == "google.com" || (Email != null && Email.EndsWith("@gmail.com", StringComparison.OrdinalIgnoreCase));
    public bool IsAuthenticated => !string.IsNullOrWhiteSpace(Uid) && Uid != "local_default_user";
}

public interface IAuthService
{
    UserSession? CurrentUser { get; }
    bool IsLoggedIn { get; }

    event EventHandler<UserSession?>? AuthStateChanged;

    Task InitializeAsync();
    Task<UserSession> SignInWithGoogleAsync(CancellationToken cancellationToken = default);
    Task<UserSession> SignInWithGoogleTokenAsync(string googleIdToken);
    Task<UserSession> SignInWithGoogleAccountAsync(string email, string? displayName = null, string? photoUrl = null);
    Task<UserSession> SignInWithEmailPasswordAsync(string email, string password);
    Task<UserSession> SignUpWithEmailPasswordAsync(string email, string password);
    Task<UserSession> SignInAnonymouslyAsync();
    Task<UserSession> UseDemoAccountAsync();
    Task SignOutAsync();
    Task<string?> GetValidTokenAsync(CancellationToken cancellationToken = default);
}
