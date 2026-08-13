namespace HashGuard.Desktop.Models;

public sealed record HashResult(
    string FilePath,
    long FileSizeBytes,
    string Sha256,
    string Sha512);
