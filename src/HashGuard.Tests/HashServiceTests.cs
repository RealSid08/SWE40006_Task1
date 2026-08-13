using System.Security.Cryptography;
using System.Text;
using HashGuard.Desktop.Services;

namespace HashGuard.Tests;

[TestClass]
public sealed class HashServiceTests
{
    [TestMethod]
    public async Task ComputeAsync_ReturnsExpectedSha256AndSha512()
    {
        var path = Path.Combine(Path.GetTempPath(), $"hashguard-{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(path, "SWE40006 HashGuard test", Encoding.UTF8);

        try
        {
            var bytes = await File.ReadAllBytesAsync(path);
            var expected256 = Convert.ToHexString(SHA256.HashData(bytes));
            var expected512 = Convert.ToHexString(SHA512.HashData(bytes));

            var result = await new HashService().ComputeAsync(path);

            Assert.AreEqual(expected256, result.Sha256);
            Assert.AreEqual(expected512, result.Sha512);
            Assert.IsTrue(HashService.MatchesExpected(expected256.ToLowerInvariant(), result));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void MatchesExpected_RejectsMalformedOrDifferentValues()
    {
        var result = new HashGuard.Desktop.Models.HashResult("sample.bin", 1, new string('A', 64), new string('B', 128));

        Assert.IsFalse(HashService.MatchesExpected("not-a-hash", result));
        Assert.IsFalse(HashService.MatchesExpected(new string('C', 64), result));
    }
}
