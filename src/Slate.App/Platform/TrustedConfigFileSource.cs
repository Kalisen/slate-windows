using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using Slate.Core.Configuration;

namespace Slate.App.Platform;

/// <summary>
/// Reads config files only when they cannot have been modified by other users: the file must be owned by the current
/// user, an administrator or the system, and only those may write to it. Symbolic links are resolved and the target is
/// checked. This mirrors the macOS check (owned by the user, not group/world writable).
/// </summary>
internal sealed class TrustedConfigFileSource : IConfigFileSource
{
    private const FileSystemRights WriteRights =
        FileSystemRights.WriteData | FileSystemRights.AppendData | FileSystemRights.WriteAttributes |
        FileSystemRights.WriteExtendedAttributes | FileSystemRights.Delete | FileSystemRights.ChangePermissions |
        FileSystemRights.TakeOwnership;

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private readonly HashSet<SecurityIdentifier> trusted;

    public TrustedConfigFileSource()
    {
        using var identity = WindowsIdentity.GetCurrent();
        trusted =
        [
            identity.User!,
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
            // TrustedInstaller
            new SecurityIdentifier("S-1-5-80-956008885-3418522649-1831038044-1853292631-2271478464"),
        ];
    }

    public ConfigFileResult Read(string path)
    {
        try
        {
            var file = new FileInfo(path);
            if (!file.Exists) return Directory.Exists(path) ? ConfigFileResult.Untrusted($"{path} is not a regular file") : ConfigFileResult.Missing;
            if (file.LinkTarget is not null)
            {
                var target = file.ResolveLinkTarget(returnFinalTarget: true);
                if (target is not FileInfo { Exists: true } resolved) return ConfigFileResult.Missing;
                file = resolved;
            }

            string? problem = TrustProblem(file);
            if (problem is not null) return ConfigFileResult.Untrusted($"{path} {problem}");

            byte[] bytes = File.ReadAllBytes(file.FullName);
            try
            {
                string text = StrictUtf8.GetString(bytes);
                return ConfigFileResult.Loaded(text.TrimStart('﻿'));
            }
            catch (DecoderFallbackException)
            {
                return ConfigFileResult.Untrusted($"{path} is not valid UTF-8");
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ConfigFileResult.Untrusted($"{path} cannot be read: {ex.Message}");
        }
    }

    private string? TrustProblem(FileInfo file)
    {
        FileSecurity security = file.GetAccessControl(AccessControlSections.Owner | AccessControlSections.Access);
        if (security.GetOwner(typeof(SecurityIdentifier)) is not SecurityIdentifier owner || !trusted.Contains(owner))
            return "is not owned by the current user";
        foreach (FileSystemAccessRule rule in security.GetAccessRules(true, true, typeof(SecurityIdentifier)))
        {
            if (rule.AccessControlType != AccessControlType.Allow || (rule.FileSystemRights & WriteRights) == 0) continue;
            if (rule.IdentityReference is SecurityIdentifier sid && !trusted.Contains(sid))
            {
                string who = TryTranslate(sid);
                return $"can be modified by {who}. Remove that permission, e.g.: icacls \"{file.FullName}\" /remove \"{who}\"";
            }
        }
        return null;
    }

    private static string TryTranslate(SecurityIdentifier sid)
    {
        try
        {
            return sid.Translate(typeof(NTAccount)).Value;
        }
        catch (IdentityNotMappedException)
        {
            return sid.Value;
        }
    }
}
