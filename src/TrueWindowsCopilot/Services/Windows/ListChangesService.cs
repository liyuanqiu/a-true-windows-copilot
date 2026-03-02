using System.Text.Json;
using TrueWindowsCopilot.Services.AI;

namespace TrueWindowsCopilot.Services.Windows;

/// <summary>
/// Lists all recorded system changes with their revert status.
/// </summary>
public class ListChangesService : IWindowsTool
{
    private readonly ChangeLogService _changeLog;

    public ListChangesService(ChangeLogService changeLog)
    {
        _changeLog = changeLog;
    }

    public string Name => "list_changes";

    public string Description =>
        "Lists all system changes made in this session with their revert status. " +
        "Shows change ID, description, timestamp, and whether it has been reverted.";

    public object ParameterSchema => new
    {
        type = "object",
        properties = new Dictionary<string, object>
        {
            ["pending_only"] = new
            {
                type = "boolean",
                description = "If true, only show changes that haven't been reverted yet (default: false)"
            }
        },
        required = Array.Empty<string>()
    };

    public Task<string> ExecuteAsync(JsonElement parameters)
    {
        var pendingOnly = parameters.TryGetProperty("pending_only", out var p) && p.GetBoolean();

        var changes = pendingOnly ? _changeLog.GetPending() : _changeLog.GetAll();

        var result = changes.Select(c => new
        {
            id = c.Id,
            description = c.Description,
            timestamp = c.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
            is_reverted = c.IsReverted,
            has_revert_script = !string.IsNullOrWhiteSpace(c.RevertScript)
        }).ToList();

        return Task.FromResult(JsonSerializer.Serialize(new
        {
            total = result.Count,
            pending = changes.Count(c => !c.IsReverted),
            changes = result
        }));
    }
}
