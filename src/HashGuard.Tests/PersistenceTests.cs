using HashGuard.Desktop.Models;
using HashGuard.Desktop.Services;

namespace HashGuard.Tests;

[TestClass]
public sealed class PersistenceTests
{
    [TestMethod]
    public async Task HistoryStore_RoundTripsJsonUsingNewtonsoftDependency()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"hashguard-tests-{Guid.NewGuid():N}");
        var historyPath = Path.Combine(directory, "history.json");
        var store = new HistoryStore(historyPath);
        var expected = new HashRecord(DateTimeOffset.Now, "demo.txt", "C:\\demo.txt", 42, new string('A', 64), new string('B', 128), "MATCH");

        try
        {
            await store.SaveAsync([expected]);
            var actual = await store.LoadAsync();

            Assert.HasCount(1, actual);
            Assert.AreEqual(expected.FileName, actual[0].FileName);
            Assert.AreEqual(expected.Sha256, actual[0].Sha256);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }

    [TestMethod]
    public async Task CsvExport_WritesReadableHeadersAndData()
    {
        var path = Path.Combine(Path.GetTempPath(), $"hashguard-{Guid.NewGuid():N}.csv");
        var record = new HashRecord(DateTimeOffset.Now, "demo.txt", "C:\\demo.txt", 42, new string('A', 64), new string('B', 128), "Not compared");

        try
        {
            await new CsvExportService().ExportAsync(path, [record]);
            var csv = await File.ReadAllTextAsync(path);

            StringAssert.Contains(csv, "FileName");
            StringAssert.Contains(csv, "demo.txt");
        }
        finally
        {
            File.Delete(path);
        }
    }
}
