using System.Security.Cryptography;
using HashGuard.Desktop.Models;

namespace HashGuard.Desktop.Services;

public sealed class HashService
{
    private const int BufferSize = 1024 * 1024;

    public async Task<HashResult> ComputeAsync(
        string filePath,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var fileInfo = new FileInfo(filePath);
        if (!fileInfo.Exists)
        {
            throw new FileNotFoundException("The selected file no longer exists.", filePath);
        }

        using var sha256 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using var sha512 = IncrementalHash.CreateHash(HashAlgorithmName.SHA512);
        await using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            BufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        var buffer = new byte[BufferSize];
        long totalRead = 0;
        int bytesRead;

        while ((bytesRead = await stream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            sha256.AppendData(buffer, 0, bytesRead);
            sha512.AppendData(buffer, 0, bytesRead);
            totalRead += bytesRead;
            progress?.Report(fileInfo.Length == 0 ? 100 : (int)(totalRead * 100 / fileInfo.Length));
        }

        progress?.Report(100);
        return new HashResult(
            fileInfo.FullName,
            fileInfo.Length,
            Convert.ToHexString(sha256.GetHashAndReset()),
            Convert.ToHexString(sha512.GetHashAndReset()));
    }

    public static bool MatchesExpected(string? expectedHash, HashResult result)
    {
        if (string.IsNullOrWhiteSpace(expectedHash))
        {
            return false;
        }

        var normalized = new string(expectedHash.Where(Uri.IsHexDigit).ToArray()).ToUpperInvariant();
        return normalized.Length switch
        {
            64 => string.Equals(normalized, result.Sha256, StringComparison.Ordinal),
            128 => string.Equals(normalized, result.Sha512, StringComparison.Ordinal),
            _ => false,
        };
    }
}
