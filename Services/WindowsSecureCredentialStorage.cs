using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace FinPulse.Windows.Services;

/// <summary>
/// Production-ready secure credential storage for FinPulse Windows.
/// Uses Windows.Security.Credentials.PasswordVault (Windows Credential Locker)
/// with a transparent DPAPI (ProtectedData) fallback for headless/test environments.
/// Ensures refresh tokens and credentials are never stored in plaintext.
/// </summary>
public class WindowsSecureCredentialStorage : ISecureCredentialStorage
{
    private const string VaultResource = "FinPulse.FirebaseAuth";
    private static readonly byte[] Entropy = "FinPulse.DPAPI.AuthEntropy.v1"u8.ToArray();

    private readonly string _fallbackFilePath;

    public WindowsSecureCredentialStorage(string? customDataDir = null)
    {
        string baseDir = customDataDir ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FinPulseCompanion");

        Directory.CreateDirectory(baseDir);
        _fallbackFilePath = Path.Combine(baseDir, "auth.secure");

        // Clean up legacy plaintext session.json if present
        try
        {
            string legacyPath = Path.Combine(baseDir, "session.json");
            if (File.Exists(legacyPath))
            {
                File.Delete(legacyPath);
            }
        }
        catch { }
    }

    public Task SaveSessionAsync(UserSession session)
    {
        if (session == null || string.IsNullOrWhiteSpace(session.Uid))
        {
            return ClearSessionAsync();
        }

        string json = JsonSerializer.Serialize(session);

        // 1. Attempt Windows Credential Locker (PasswordVault)
        bool savedToVault = false;
        try
        {
            var vault = new global::Windows.Security.Credentials.PasswordVault();
            // Clear prior credentials for resource to prevent duplicates
            try
            {
                var existing = vault.FindAllByResource(VaultResource);
                foreach (var cred in existing)
                {
                    vault.Remove(cred);
                }
            }
            catch { }

            var credential = new global::Windows.Security.Credentials.PasswordCredential(
                VaultResource,
                session.Uid,
                json);

            vault.Add(credential);
            savedToVault = true;
        }
        catch
        {
            // PasswordVault may be unavailable in some virtualized/test runtimes
            savedToVault = false;
        }

        // 2. Also save to DPAPI protected file as resilient backup
        try
        {
            byte[] plainBytes = Encoding.UTF8.GetBytes(json);
            byte[] encrypted = ProtectedData.Protect(plainBytes, Entropy, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(_fallbackFilePath, encrypted);
        }
        catch
        {
            if (!savedToVault)
            {
                throw;
            }
        }

        return Task.CompletedTask;
    }

    public Task<UserSession?> LoadSessionAsync()
    {
        // 1. Try Windows PasswordVault
        try
        {
            var vault = new global::Windows.Security.Credentials.PasswordVault();
            var credentials = vault.FindAllByResource(VaultResource);
            foreach (var cred in credentials)
            {
                cred.RetrievePassword();
                if (!string.IsNullOrWhiteSpace(cred.Password))
                {
                    var session = JsonSerializer.Deserialize<UserSession>(cred.Password);
                    if (session != null && !string.IsNullOrWhiteSpace(session.Uid))
                    {
                        return Task.FromResult<UserSession?>(session);
                    }
                }
            }
        }
        catch
        {
            // PasswordVault empty or unavailable, proceed to DPAPI fallback
        }

        // 2. Try DPAPI protected file
        try
        {
            if (File.Exists(_fallbackFilePath))
            {
                byte[] encrypted = File.ReadAllBytes(_fallbackFilePath);
                byte[] plainBytes = ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.CurrentUser);
                string json = Encoding.UTF8.GetString(plainBytes);
                var session = JsonSerializer.Deserialize<UserSession>(json);
                if (session != null && !string.IsNullOrWhiteSpace(session.Uid))
                {
                    return Task.FromResult<UserSession?>(session);
                }
            }
        }
        catch
        {
            // File corrupted or tampered; clean up
            try { File.Delete(_fallbackFilePath); } catch { }
        }

        return Task.FromResult<UserSession?>(null);
    }

    public Task ClearSessionAsync()
    {
        // 1. Clear PasswordVault
        try
        {
            var vault = new global::Windows.Security.Credentials.PasswordVault();
            var credentials = vault.FindAllByResource(VaultResource);
            foreach (var cred in credentials)
            {
                vault.Remove(cred);
            }
        }
        catch { }

        // 2. Clear DPAPI file
        try
        {
            if (File.Exists(_fallbackFilePath))
            {
                File.Delete(_fallbackFilePath);
            }
        }
        catch { }

        return Task.CompletedTask;
    }
}
