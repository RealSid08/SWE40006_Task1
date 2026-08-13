using HashGuard.Desktop.Models;
using Newtonsoft.Json;

namespace HashGuard.Desktop.Services;

public sealed class HistoryStore
{
    private const int MaximumRecords = 50;
    private readonly string _historyPath;

    public HistoryStore(string? historyPath = null)
    {
        _historyPath = historyPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HashGuard Desktop",
            "history.json");
    }

    public async Task<IReadOnlyList<HashRecord>> LoadAsync()
    {
        if (!File.Exists(_historyPath))
        {
            return Array.Empty<HashRecord>();
        }

        try
        {
            var json = await File.ReadAllTextAsync(_historyPath);
            return JsonConvert.DeserializeObject<List<HashRecord>>(json) ?? [];
        }
        catch (JsonException)
        {
            return Array.Empty<HashRecord>();
        }
    }

    public async Task SaveAsync(IEnumerable<HashRecord> records)
    {
        var directory = Path.GetDirectoryName(_historyPath)
            ?? throw new InvalidOperationException("History path has no parent directory.");
        Directory.CreateDirectory(directory);

        var trimmed = records.Take(MaximumRecords).ToList();
        var json = JsonConvert.SerializeObject(trimmed, Formatting.Indented);
        await File.WriteAllTextAsync(_historyPath, json);
    }

    public Task ClearAsync()
    {
        if (File.Exists(_historyPath))
        {
            File.Delete(_historyPath);
        }

        return Task.CompletedTask;
    }
}
