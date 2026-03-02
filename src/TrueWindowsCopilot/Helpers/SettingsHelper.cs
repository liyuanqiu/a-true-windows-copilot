using System.Text.Json;

namespace TrueWindowsCopilot.Helpers;

public class SettingsHelper
{
    private readonly string _settingsPath;

    public string ApiKey { get; set; } = "";
    public string ApiBaseUrl { get; set; } = "https://api.openai.com/v1";
    public string ModelName { get; set; } = "gpt-5.2";

    public SettingsHelper()
    {
        _settingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        Load();
    }

    public void Load()
    {
        if (!File.Exists(_settingsPath)) return;

        try
        {
            var json = File.ReadAllText(_settingsPath);
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("OpenAI", out var openAi))
            {
                if (openAi.TryGetProperty("ApiKey", out var key) && key.GetString() is { Length: > 0 } k)
                    ApiKey = k;
                if (openAi.TryGetProperty("ApiBaseUrl", out var url) && url.GetString() is { Length: > 0 } u)
                    ApiBaseUrl = u;
                if (openAi.TryGetProperty("ModelName", out var model) && model.GetString() is { Length: > 0 } m)
                    ModelName = m;
            }
        }
        catch
        {
            // appsettings.json missing or malformed — use hard-coded defaults
        }
    }

    public void Save()
    {
        var data = new Dictionary<string, object>
        {
            ["OpenAI"] = new Dictionary<string, string>
            {
                ["ApiKey"] = ApiKey,
                ["ApiBaseUrl"] = ApiBaseUrl,
                ["ModelName"] = ModelName
            }
        };

        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_settingsPath, json);
    }
}
