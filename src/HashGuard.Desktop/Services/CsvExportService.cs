using System.Globalization;
using CsvHelper;
using HashGuard.Desktop.Models;

namespace HashGuard.Desktop.Services;

public sealed class CsvExportService
{
    public async Task ExportAsync(string outputPath, IEnumerable<HashRecord> records)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        await using var writer = new StreamWriter(outputPath, false, new System.Text.UTF8Encoding(true));
        await using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
        await csv.WriteRecordsAsync(records);
    }
}
