using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Slate.Core.Configuration;

/// <summary>What the user approves: the loaded config files and the lines that run shell commands.</summary>
public sealed class ConfigReview
{
    private const string ShellKeyword = "shell";
    private readonly List<string> paths = [];
    private readonly List<string> shellLines = [];
    private readonly List<byte> framed = [];

    public IReadOnlyList<string> Paths => paths;
    public IReadOnlyList<string> ShellLines => shellLines;
    public bool IsEmpty => paths.Count == 0;

    /// <summary>Adds a file. Path and contents are length-prefixed so content cannot shift between files unnoticed.</summary>
    public void AddFile(string path, string contents)
    {
        paths.Add(path);
        var length = new byte[sizeof(ulong)];
        foreach (string part in new[] { path, contents })
        {
            byte[] bytes = Encoding.UTF8.GetBytes(part);
            BinaryPrimitives.WriteUInt64BigEndian(length, (ulong)bytes.Length);
            framed.AddRange(length);
            framed.AddRange(bytes);
        }
    }

    public void AddLine(string line)
    {
        if (line.Contains(ShellKeyword, StringComparison.OrdinalIgnoreCase)) shellLines.Add(line);
    }

    /// <summary>Hex SHA-256 of every file added, in order.</summary>
    public string Digest() => Convert.ToHexStringLower(SHA256.HashData([.. framed]));
}

/// <summary>Remembers the digest of the last approved configuration.</summary>
public interface IApprovalStore
{
    string? ApprovedDigest();

    void StoreApprovedDigest(string digest);
}

/// <summary>Asks the user whether a changed configuration may run.</summary>
public interface IConfigApprover
{
    bool Approve(ConfigReview review);
}

/// <summary>A configuration runs only if it is unchanged since the user last approved it, or the user approves it now.</summary>
public sealed class ConfigApproval(IApprovalStore store, IConfigApprover approver)
{
    public bool IsApproved(ConfigReview review)
    {
        string digest = review.Digest();
        if (digest == store.ApprovedDigest()) return true;
        if (!approver.Approve(review)) return false;
        store.StoreApprovedDigest(digest);
        return true;
    }
}
