using System;
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

    public bool IsAuthenticated => !string.IsNullOrEmpty(Uid);
}

public interface IAuthService
{
    UserSession? CurrentUser { get; }
    bool IsLoggedIn { get; }

    event EventHandler<UserSession?>? AuthStateChanged;

    Task InitializeAsync();
    Task<UserSession> SignInWithEmailPasswordAsync(string email, string password);
    Task<UserSession> SignUpWithEmailPasswordAsync(string email, string password);
    Task<UserSession> SignInAnonymouslyAsync();
    Task<UserSession> SignInWithGoogleTokenAsync(string idToken);
    Task<UserSession> UseDemoAccountAsync();
    Task SignOutAsync();
    Task<string?> GetValidTokenAsync();
}
