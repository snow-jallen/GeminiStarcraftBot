using System.Text.Json;

namespace Shared.Utils;

public class GameRecord
{
    public DateTime Timestamp { get; set; }
    public string MapName { get; set; } = "";
    public int PlayerCount { get; set; }
    public string Race { get; set; } = "";
    public string Result { get; set; } = ""; // "Win", "Loss", "Draw"
    public string Duration { get; set; } = "";
    public int Minerals { get; set; }
    public int Gas { get; set; }
    public int SupplyUsed { get; set; }
    public int SupplyTotal { get; set; }
    public int WorkerCount { get; set; }
    public int ArmyCount { get; set; }
    public string BuildOrder { get; set; } = "";
}

public static class GameStatLogger
{
    private static readonly string _filePath = "game_stats.json";

    public static void LogGameResult(GameRecord record)
    {
        var history = GetGameHistory();
        history.Add(record);
        // Save newest first
        history = history.OrderByDescending(x => x.Timestamp).ToList();

        var json = JsonSerializer.Serialize(history, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_filePath, json);
    }

    public static List<GameRecord> GetGameHistory()
    {
        if (!File.Exists(_filePath))
        {
            return new List<GameRecord>();
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            var result = JsonSerializer.Deserialize<List<GameRecord>>(json);
            return result ?? new List<GameRecord>();
        }
        catch
        {
            return new List<GameRecord>();
        }
    }
}
