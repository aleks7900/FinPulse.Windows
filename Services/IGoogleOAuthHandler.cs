using System.Threading;
using System.Threading.Tasks;

namespace FinPulse.Windows.Services;

public class GoogleOAuthResult
{
    public bool IsSuccess { get; set; }
    public string? IdToken { get; set; }
    public string? AccessToken { get; set; }
    public string? ErrorMessage { get; set; }
    public bool IsCancelled { get; set; }
}

public interface IGoogleOAuthHandler
{
    Task<GoogleOAuthResult> AuthenticateViaBrowserAsync(CancellationToken cancellationToken = default);
}
