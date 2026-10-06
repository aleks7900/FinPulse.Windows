using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace FinPulse.Windows.Services;

/// <summary>
/// Handles production-ready Google OAuth 2.0 PKCE Authorization Code flow
/// for installed Windows desktop applications (RFC 8252).
/// Opens the user's default system browser, uses loopback callback on a dynamic port,
/// enforces PKCE and cryptographic state verification, and exchanges the authorization
/// code for Google credentials.
/// </summary>
public class GoogleOAuthHandler : IGoogleOAuthHandler
{
    private readonly HttpClient _httpClient;

    public GoogleOAuthHandler(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
    }

    public async Task<GoogleOAuthResult> AuthenticateViaBrowserAsync(CancellationToken cancellationToken = default)
    {
        // 1. Generate PKCE code verifier and code challenge
        string codeVerifier = GenerateCodeVerifier();
        string codeChallenge = GenerateCodeChallenge(codeVerifier);

        // 2. Generate secure random state
        string state = GenerateCryptoState();

        // 3. Dynamically allocate an open loopback port
        int port = GetRandomUnusedPort();
        string redirectUri = $"http://127.0.0.1:{port}/";

        HttpListener? listener = null;
        try
        {
            listener = new HttpListener();
            listener.Prefixes.Add(redirectUri);
            listener.Start();

            // 4. Construct Authorization URL
            string authUrl = $"{FirebaseConfig.GoogleAuthUri}?" +
                             $"client_id={Uri.EscapeDataString(FirebaseConfig.GoogleDesktopClientId)}&" +
                             $"redirect_uri={Uri.EscapeDataString(redirectUri)}&" +
                             $"response_type=code&" +
                             $"scope={Uri.EscapeDataString("openid email profile")}&" +
                             $"code_challenge={Uri.EscapeDataString(codeChallenge)}&" +
                             $"code_challenge_method=S256&" +
                             $"state={Uri.EscapeDataString(state)}";

            // 5. Open Default System Browser
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = authUrl,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                return new GoogleOAuthResult
                {
                    IsSuccess = false,
                    ErrorMessage = $"Could not launch system browser: {ex.Message}"
                };
            }

            // 6. Wait for loopback callback with timeout / cancellation
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            linkedCts.CancelAfter(TimeSpan.FromMinutes(3)); // 3-minute timeout

            using var reg = linkedCts.Token.Register(() =>
            {
                try { listener.Stop(); } catch { }
            });

            HttpListenerContext context;
            try
            {
                context = await listener.GetContextAsync();
            }
            catch (Exception) when (linkedCts.Token.IsCancellationRequested)
            {
                return new GoogleOAuthResult
                {
                    IsSuccess = false,
                    IsCancelled = true,
                    ErrorMessage = cancellationToken.IsCancellationRequested
                        ? "Google Sign-In was cancelled."
                        : "Google Sign-In timed out waiting for browser completion."
                };
            }

            var query = context.Request.QueryString;
            string? error = query["error"];
            string? returnedState = query["state"];
            string? code = query["code"];

            // 7. Check for user cancellation or errors from Google
            if (!string.IsNullOrEmpty(error))
            {
                await SendHtmlResponseAsync(context.Response, false, "Sign-in cancelled in browser.");
                return new GoogleOAuthResult
                {
                    IsSuccess = false,
                    IsCancelled = error.Equals("access_denied", StringComparison.OrdinalIgnoreCase),
                    ErrorMessage = $"Google authorization error: {error}"
                };
            }

            // 8. Validate state
            if (!string.Equals(state, returnedState, StringComparison.Ordinal))
            {
                await SendHtmlResponseAsync(context.Response, false, "Security error: invalid OAuth state parameter.");
                return new GoogleOAuthResult
                {
                    IsSuccess = false,
                    ErrorMessage = "Invalid OAuth state. Possible CSRF attempt rejected."
                };
            }

            if (string.IsNullOrWhiteSpace(code))
            {
                await SendHtmlResponseAsync(context.Response, false, "No authorization code returned.");
                return new GoogleOAuthResult
                {
                    IsSuccess = false,
                    ErrorMessage = "Missing authorization code from Google callback."
                };
            }

            // Send successful browser response
            await SendHtmlResponseAsync(context.Response, true, "Sign-in successful. You can close this tab.");

            // 9. Exchange authorization code with Google token endpoint
            var tokenResult = await ExchangeCodeForTokensAsync(code, codeVerifier, redirectUri, cancellationToken);
            return tokenResult;
        }
        finally
        {
            try
            {
                listener?.Stop();
                listener?.Close();
            }
            catch { }
        }
    }

    private async Task<GoogleOAuthResult> ExchangeCodeForTokensAsync(
        string code,
        string codeVerifier,
        string redirectUri,
        CancellationToken cancellationToken)
    {
        var postData = new Dictionary<string, string>
        {
            ["client_id"] = FirebaseConfig.GoogleDesktopClientId,
            ["code"] = code,
            ["code_verifier"] = codeVerifier,
            ["grant_type"] = "authorization_code",
            ["redirect_uri"] = redirectUri
        };

        // Note: For public desktop installed applications using PKCE,
        // client_secret is omitted in accordance with RFC 8252 Section 8.5.

        using var requestContent = new FormUrlEncodedContent(postData);
        using var response = await _httpClient.PostAsync(FirebaseConfig.GoogleTokenUri, requestContent, cancellationToken);
        string responseJson = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            string errorDesc = "Failed to exchange authorization code for tokens";
            try
            {
                using var doc = JsonDocument.Parse(responseJson);
                if (doc.RootElement.TryGetProperty("error_description", out var desc))
                {
                    errorDesc = desc.GetString() ?? errorDesc;
                }
                else if (doc.RootElement.TryGetProperty("error", out var err))
                {
                    errorDesc = err.GetString() ?? errorDesc;
                }
            }
            catch { }

            return new GoogleOAuthResult
            {
                IsSuccess = false,
                ErrorMessage = errorDesc
            };
        }

        try
        {
            using var doc = JsonDocument.Parse(responseJson);
            var root = doc.RootElement;
            string? idToken = root.TryGetProperty("id_token", out var itProp) ? itProp.GetString() : null;
            string? accessToken = root.TryGetProperty("access_token", out var atProp) ? atProp.GetString() : null;

            if (string.IsNullOrWhiteSpace(idToken))
            {
                return new GoogleOAuthResult
                {
                    IsSuccess = false,
                    ErrorMessage = "Google token endpoint response did not contain an ID token."
                };
            }

            return new GoogleOAuthResult
            {
                IsSuccess = true,
                IdToken = idToken,
                AccessToken = accessToken
            };
        }
        catch (Exception ex)
        {
            return new GoogleOAuthResult
            {
                IsSuccess = false,
                ErrorMessage = $"Failed to parse Google token response: {ex.Message}"
            };
        }
    }

    public static int GetRandomUnusedPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    public static string GenerateCodeVerifier()
    {
        byte[] bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Base64UrlEncode(bytes);
    }

    public static string GenerateCodeChallenge(string codeVerifier)
    {
        byte[] verifierBytes = Encoding.ASCII.GetBytes(codeVerifier);
        byte[] hash = SHA256.HashData(verifierBytes);
        return Base64UrlEncode(hash);
    }

    public static string GenerateCryptoState()
    {
        byte[] bytes = new byte[16];
        RandomNumberGenerator.Fill(bytes);
        return Base64UrlEncode(bytes);
    }

    public static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static async Task SendHtmlResponseAsync(HttpListenerResponse response, bool isSuccess, string message)
    {
        string title = isSuccess ? "Sign-In Complete" : "Sign-In Status";
        string heading = isSuccess ? "✓ Signed in to FinPulse" : "Sign-In Attention";
        string subtext = isSuccess
            ? "Authentication complete. You can close this browser tab and return to FinPulse Companion on Windows."
            : message;
        string badgeColor = isSuccess ? "#10b981" : "#ef4444";
        string badgeSymbol = isSuccess ? "&#10003;" : "&#10005;";

        string html = $@"<!DOCTYPE html>
<html>
<head>
  <meta charset=""utf-8"">
  <meta name=""viewport"" content=""width=device-width, initial-scale=1"">
  <title>FinPulse - {title}</title>
  <style>
    body {{
      background-color: #0f172a;
      color: #f8fafc;
      font-family: -apple-system, BlinkMacSystemFont, ""Segoe UI"", Roboto, Helvetica, Arial, sans-serif;
      display: flex;
      align-items: center;
      justify-content: center;
      min-height: 100vh;
      margin: 0;
    }}
    .card {{
      background: #1e293b;
      border: 1px solid #334155;
      border-radius: 16px;
      padding: 40px;
      text-align: center;
      max-width: 440px;
      box-shadow: 0 20px 25px -5px rgba(0, 0, 0, 0.5);
    }}
    .badge {{
      background: {badgeColor};
      color: white;
      font-size: 28px;
      width: 56px;
      height: 56px;
      border-radius: 50%;
      display: inline-flex;
      align-items: center;
      justify-content: center;
      margin-bottom: 20px;
    }}
    h2 {{ margin: 0 0 12px 0; font-size: 22px; font-weight: 600; }}
    p {{ margin: 0; color: #94a3b8; font-size: 14px; line-height: 1.6; }}
  </style>
</head>
<body>
  <div class=""card"">
    <div class=""badge"">{badgeSymbol}</div>
    <h2>{heading}</h2>
    <p>{subtext}</p>
  </div>
</body>
</html>";

        byte[] buffer = Encoding.UTF8.GetBytes(html);
        response.ContentType = "text/html; charset=utf-8";
        response.ContentLength64 = buffer.Length;
        try
        {
            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            response.OutputStream.Close();
        }
        catch { }
    }
}
