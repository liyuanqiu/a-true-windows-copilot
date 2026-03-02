using System.Diagnostics;
using System.Text;
using System.Text.Json;
using TrueWindowsCopilot.Services.AI;

namespace TrueWindowsCopilot.Services.Windows;

/// <summary>
/// Executes a system-modifying PowerShell script AND records a revert script.
/// The AI must provide both a change script and a revert script.
/// Use this instead of 'powershell' for any operation that modifies the system.
/// </summary>
public class SystemChangeService : IWindowsTool
{
    private readonly ChangeLogService _changeLog;

    public SystemChangeService(ChangeLogService changeLog)
    {
        _changeLog = changeLog;
    }

    public string Name => "system_change";

    public string Description =>
        "Executes a system-modifying PowerShell script AND records a revert script so the change can be undone. " +
        "You MUST use this tool (instead of 'powershell') for ANY operation that modifies the system: " +
        "killing processes, changing settings, modifying files, editing registry, managing services, etc. " +
        "You must provide a description of the change, the script to execute, AND the revert script to undo it. " +
        "If the operation is irreversible (e.g., uninstalling an app, emptying recycle bin, permanently deleting files), " +
        "set irreversible=true — this will ask the user for confirmation before executing.";

    public object ParameterSchema => new
    {
        type = "object",
        properties = new Dictionary<string, object>
        {
            ["description"] = new
            {
                type = "string",
                description = "A short human-readable description of what this change does (e.g., 'Switch to dark mode', 'Kill notepad process')"
            },
            ["script"] = new
            {
                type = "string",
                description = "The PowerShell script that performs the change."
            },
            ["revert_script"] = new
            {
                type = "string",
                description = "The PowerShell script that reverts/undoes this change. Must restore the system to its previous state. " +
                              "For reversible changes, this should fully undo the operation. " +
                              "For irreversible changes, set to empty string."
            },
            ["irreversible"] = new
            {
                type = "boolean",
                description = "Set to true if this operation CANNOT be undone (e.g., uninstall app, empty recycle bin, permanent delete). " +
                              "This will require user confirmation before executing. Default: false."
            },
            ["timeout_seconds"] = new
            {
                type = "integer",
                description = "Maximum execution time in seconds (default: 30, max: 300)"
            }
        },
        required = new[] { "description", "script", "revert_script" }
    };

    public bool RequiresConfirmation => true;

    public async Task<string> ExecuteAsync(JsonElement parameters)
    {
        var description = parameters.GetProperty("description").GetString() ?? "";
        var script = parameters.GetProperty("script").GetString();
        var revertScript = parameters.GetProperty("revert_script").GetString() ?? "";
        var irreversible = parameters.TryGetProperty("irreversible", out var ir) && ir.GetBoolean();

        if (string.IsNullOrWhiteSpace(script))
            return JsonSerializer.Serialize(new { error = "Script is required" });

        var timeoutSeconds = parameters.TryGetProperty("timeout_seconds", out var t) ? t.GetInt32() : 30;
        timeoutSeconds = Math.Clamp(timeoutSeconds, 1, 300);

        // If irreversible, request user confirmation before executing
        if (irreversible)
        {
            // Store pending change for confirmation
            PendingChange = new PendingChangeInfo(description, script, revertScript, timeoutSeconds);
            return JsonSerializer.Serialize(new
            {
                requires_confirmation = true,
                description,
                irreversible = true,
                message = "This operation is irreversible and requires user confirmation. Waiting for user response..."
            });
        }

        return await ExecuteChange(description, script, revertScript, timeoutSeconds);
    }

    /// <summary>
    /// Stores a pending irreversible change waiting for user confirmation.
    /// </summary>
    public PendingChangeInfo? PendingChange { get; set; }

    /// <summary>
    /// Executes a confirmed change (called directly for reversible, or after confirmation for irreversible).
    /// </summary>
    public async Task<string> ExecuteChange(string description, string script, string revertScript, int timeoutSeconds)
    {
        try
        {
            var output = await RunScriptAsync(script, timeoutSeconds);

            // Record the change
            var change = _changeLog.Record(description, script, revertScript, output.Output);

            return JsonSerializer.Serialize(new
            {
                success = output.ExitCode == 0,
                exit_code = output.ExitCode,
                change_id = change.Id,
                description,
                output = string.IsNullOrEmpty(output.Output) ? null : output.Output,
                error = string.IsNullOrEmpty(output.Error) ? null : output.Error,
                revert_available = !string.IsNullOrWhiteSpace(revertScript),
                message = $"Change #{change.Id} recorded. Can be reverted with revert_change tool."
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    public record PendingChangeInfo(string Description, string Script, string RevertScript, int TimeoutSeconds);

    internal static async Task<ScriptOutput> RunScriptAsync(string script, int timeoutSeconds)
    {
        var encodedScript = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));

        var psi = new ProcessStartInfo
        {
            FileName = PowerShellService.GetPowerShellExe(),
            Arguments = $"-NoProfile -NoLogo -EncodedCommand {encodedScript}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start PowerShell process");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));

        var outputTask = process.StandardOutput.ReadToEndAsync(cts.Token);
        var errorTask = process.StandardError.ReadToEndAsync(cts.Token);

        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            var partial = outputTask.IsCompleted ? await outputTask : null;
            return new ScriptOutput(-1, partial, $"Script timed out after {timeoutSeconds} seconds");
        }

        var output = await outputTask;
        var error = await errorTask;

        if (output.Length > 50000)
            output = output[..50000] + "\n\n... (output truncated)";

        return new ScriptOutput(process.ExitCode, output.TrimEnd(), error?.TrimEnd());
    }

    internal record ScriptOutput(int ExitCode, string? Output, string? Error);
}
