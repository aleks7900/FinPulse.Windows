using System.Threading.Tasks;

namespace FinPulse.Windows.Services;

/// <summary>
/// Secure credential storage contract for FinPulse Windows.
/// Used to persist authenticated UserSession and refresh tokens securely.
/// </summary>
public interface ISecureCredentialStorage
{
    Task SaveSessionAsync(UserSession session);
    Task<UserSession?> LoadSessionAsync();
    Task ClearSessionAsync();
}
