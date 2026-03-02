using System.Text.Json;
using TrueWindowsCopilot.Services.AI;

namespace TrueWindowsCopilot.Services.Windows;

/// <summary>
/// Reverts a previously recorded system change by executing its revert script.
/// </summary>
public class RevertChangeService : IWindowsTool
{
    private readonly ChangeLogService _changeLog;

    public RevertChangeService(ChangeLogService changeLog)
    {
        _changeLog = changeLog;
    }

    public string Name => "revert_change";

    public string Description =>
        "Reverts a previously made system change by executing its recorded revert script. " +
        "Use 'list_changes' first to see available changes and their IDs. " +
        "Can also revert the most recent change by setting latest=true.";

    public object ParameterSchema => new
    {
        type = "object",
        properties = new Dictionary<string, object>
        {
            ["change_id"] = new
            {
                type = "integer",
                description = "The ID of the change to revert (from list_changes or system_change output)"
            },
            ["latest"] = new
            {
                type = "boolean",
                description = "If true, revert the most recent un-reverted change. Overrides change_id."
            }
        },
        required = Array.Empty<string>()
    };

    public async Task<string> ExecuteAsync(JsonElement parameters)
    {
        var useLatest = parameters.TryGetProperty("latest", out var l) && l.GetBoolean();
        var changeId = parameters.TryGetProperty("change_id", out var id) ? id.GetInt32() : 0;

        Models.SystemChange? change;

        if (useLatest)
        {
            change = _changeLog.GetPending().FirstOrDefault();
            if (change == null)
                return JsonSerializer.Serialize(new { error = "No un-reverted changes found." });
        }
        else if (changeId > 0)
        {
            change = _changeLog.GetById(changeId);
            if (change == null)
                return JsonSerializer.Serialize(new { error = $"Change #{changeId} not found." });
            if (change.IsReverted)
                return JsonSerializer.Serialize(new { error = $"Change #{changeId} has already been reverted." });
        }
        else
        {
            return JsonSerializer.Serialize(new { error = "Provide either change_id or set latest=true." });
        }

        if (string.IsNullOrWhiteSpace(change.RevertScript))
            return JsonSerializer.Serialize(new
            {
                error = $"Change #{change.Id} ('{change.Description}') has no revert script."
            });

        try
        {
            var output = await SystemChangeService.RunScriptAsync(change.RevertScript, 30);

            _changeLog.MarkReverted(change.Id, output.Output);

            return JsonSerializer.Serialize(new
            {
                success = output.ExitCode == 0,
                change_id = change.Id,
                description = change.Description,
                message = $"Successfully reverted change #{change.Id}: {change.Description}",
                output = string.IsNullOrEmpty(output.Output) ? null : output.Output,
                error = string.IsNullOrEmpty(output.Error) ? null : output.Error
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = $"Revert failed: {ex.Message}" });
        }
    }
}
