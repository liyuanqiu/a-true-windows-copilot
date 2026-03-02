using System.Diagnostics;
using System.Text.Json;
using TrueWindowsCopilot.Services.AI;

namespace TrueWindowsCopilot.Services.Windows;

/// <summary>
/// The universal tool — executes PowerShell commands/scripts.
/// The AI uses this to perform ANY system operation autonomously.
/// </summary>
public class PowerShellService : IWindowsTool
{
    private static readonly Lazy<string> PowerShellExe = new(() =>
    {
        // Prefer PowerShell 7+ (pwsh.exe), fallback to Windows PowerShell 5.1
        var pwsh = FindPwsh();
        return pwsh ?? "powershell.exe";
    });

    private static string? FindPwsh()
    {
        // 1. Check PATH
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "pwsh.exe",
                Arguments = "-NoProfile -Command \"$PSVersionTable.PSVersion.ToString()\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            if (p != null)
            {
                p.WaitForExit(3000);
                if (p.ExitCode == 0) return "pwsh.exe";
            }
        }
        catch { }

        // 2. Check common install locations
        string[] paths =
        [
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "PowerShell", "7", "pwsh.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "PowerShell", "8", "pwsh.exe"),
        ];
        return paths.FirstOrDefault(File.Exists);
    }

    /// <summary>
    /// Returns the PowerShell executable path (pwsh.exe if available, otherwise powershell.exe).
    /// Used by other services that need to run PowerShell scripts.
    /// </summary>
    public static string GetPowerShellExe() => PowerShellExe.Value;

    public string Name => "powershell";

    public string Description =>
        "Executes PowerShell commands or scripts on the local Windows system for READ-ONLY operations. " +
        "Use this for queries, lookups, and information gathering. " +
        "For ANY operation that MODIFIES the system (settings, files, processes, services, registry, etc.), " +
        "use the 'system_change' tool instead — it records a revert script so changes can be undone. " +
        "You can run any valid PowerShell command. Output is returned as text. " +
        "Use ConvertTo-Json for structured data. Use multi-line scripts for complex tasks.";

    public object ParameterSchema => new
    {
        type = "object",
        properties = new Dictionary<string, object>
        {
            ["script"] = new
            {
                type = "string",
                description = "PowerShell script to execute. Can be a single command or a multi-line script. " +
                              "Use semicolons or newlines to separate multiple statements."
            },
            ["timeout_seconds"] = new
            {
                type = "integer",
                description = "Maximum execution time in seconds (default: 30, max: 300)"
            }
        },
        required = new[] { "script" }
    };

    public async Task<string> ExecuteAsync(JsonElement parameters)
    {
        var script = parameters.GetProperty("script").GetString();
        if (string.IsNullOrWhiteSpace(script))
            return JsonSerializer.Serialize(new { error = "Script is required" });

        var timeoutSeconds = parameters.TryGetProperty("timeout_seconds", out var t) ? t.GetInt32() : 30;
        timeoutSeconds = Math.Clamp(timeoutSeconds, 1, 300);

        try
        {
            var output = await SystemChangeService.RunScriptAsync(script, timeoutSeconds);

            return JsonSerializer.Serialize(new
            {
                success = output.ExitCode == 0,
                exit_code = output.ExitCode,
                output = string.IsNullOrEmpty(output.Output) ? null : output.Output,
                error = string.IsNullOrEmpty(output.Error) ? null : output.Error
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }
}
