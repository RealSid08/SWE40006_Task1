namespace HashGuard.Desktop.Models;

public sealed record HashRecord(
    DateTimeOffset ScannedAt,
    string FileName,
    string FilePath,
    long FileSizeBytes,
    string Sha256,
    string Sha512,
    string VerificationStatus);
