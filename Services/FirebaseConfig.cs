namespace FinPulse.Windows.Services;

/// <summary>
/// Authoritative Firebase and Google Cloud configuration for FinPulse.
/// Shared across FinPulse Android and FinPulse Windows for 100% cloud parity.
/// </summary>
public static class FirebaseConfig
{
    // Firebase Web Application Configuration
    public const string ApiKey = "AIzaSyDcUVQMV6iVQvRu2u8FQ7LhaR5SXCbqmRQ";
    public const string ProjectId = "finpulse-cloud";
    public const string AuthDomain = "finpulse-cloud.firebaseapp.com";
    public const string StorageBucket = "finpulse-cloud.firebasestorage.app";
    public const string MessagingSenderId = "500924060314";
    public const string AppId = "1:500924060314:web:2b7297a7a477455d6f4523";
    public const string MeasurementId = "G-PB0K1DVRSB";

    // Google OAuth 2.0 Desktop Installed Application Client ID
    public const string GoogleDesktopClientId = "500924060314-1pupsto289mqs82vfl4i7ocnsub68lfe.apps.googleusercontent.com";

    // OAuth & Auth Endpoints
    public const string GoogleAuthUri = "https://accounts.google.com/o/oauth2/v2/auth";
    public const string GoogleTokenUri = "https://oauth2.googleapis.com/token";
    public const string BaseAuthUrl = "https://identitytoolkit.googleapis.com/v1/accounts";
    public const string SecureTokenUrl = "https://securetoken.googleapis.com/v1/token";
    public const string BaseFirestoreUrl = $"https://firestore.googleapis.com/v1/projects/{ProjectId}/databases/(default)/documents";
}
