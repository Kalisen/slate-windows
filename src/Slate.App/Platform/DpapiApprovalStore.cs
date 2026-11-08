using System.Security.Cryptography;
using System.Text;
using Slate.Core.Configuration;

namespace Slate.App.Platform;

/// <summary>
/// Keeps the digest of the approved config in %APPDATA%\Slate, encrypted with DPAPI for the current user (the Windows
/// counterpart of the macOS keychain entry): another account cannot forge an approval.
/// </summary>
internal sealed class DpapiApprovalStore : IApprovalStore
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("io.opus1.slate.config-approval");
    private readonly string path = Path.Combine(AppPaths.DataDirectory, AppPaths.ApprovalFileName);

    public string? ApprovedDigest()
    {
        try
        {
            if (!File.Exists(path)) return null;
            byte[] data = ProtectedData.Unprotect(File.ReadAllBytes(path), Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(data);
        }
        catch (Exception ex) when (ex is CryptographicException or IOException or UnauthorizedAccessException)
        {
            Log.Warn($"Unable to read the approved config digest: {ex.Message}");
            return null;
        }
    }

    public void StoreApprovedDigest(string digest)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.DataDirectory);
            byte[] data = ProtectedData.Protect(Encoding.UTF8.GetBytes(digest), Entropy, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(path, data);
        }
        catch (Exception ex) when (ex is CryptographicException or IOException or UnauthorizedAccessException)
        {
            Log.Error($"Unable to save the approved config digest, you will be asked again: {ex.Message}");
        }
    }
}
