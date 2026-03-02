using System.Diagnostics;
using System.Text.Json;
using Microsoft.Win32;
using TrueWindowsCopilot.Services.AI;

namespace TrueWindowsCopilot.Services.Windows;

/// <summary>
/// Launches applications by name, path, or protocol URI.
/// Can also open URLs, files with their default app, and system tools.
/// </summary>
public class AppLauncherService : IWindowsTool
{
    public string Name => "launch_application";

    public string Description =>
        "Launches an application by name, executable path, or protocol URI. " +
        "Can also open files with their default application, open URLs in the browser, " +
        "and launch built-in Windows tools (Task Manager, Device Manager, etc.).";

    public object ParameterSchema => new
    {
        type = "object",
        properties = new Dictionary<string, object>
        {
            ["target"] = new
            {
                type = "string",
                description = "The application name, executable path, URL, or protocol URI to launch. " +
                              "Examples: 'notepad', 'calc', 'C:\\Program Files\\app.exe', 'https://example.com', " +
                              "'ms-settings:', 'devmgmt.msc', 'taskmgr'"
            },
            ["arguments"] = new
            {
                type = "string",
                description = "Optional command-line arguments"
            },
            ["run_as_admin"] = new
            {
                type = "boolean",
                description = "Whether to run with administrator privileges (default: false)"
            },
            ["working_directory"] = new
            {
                type = "string",
                description = "Working directory for the launched application"
            }
        },
        required = new[] { "target" }
    };

    // Well-known app shortcuts
    private static readonly Dictionary<string, (string Exe, string? Args)> KnownApps = new(StringComparer.OrdinalIgnoreCase)
    {
        ["task manager"] = ("taskmgr.exe", null),
        ["taskmgr"] = ("taskmgr.exe", null),
        ["device manager"] = ("devmgmt.msc", null),
        ["control panel"] = ("control.exe", null),
        ["settings"] = ("ms-settings:", null),
        ["calculator"] = ("calc.exe", null),
        ["notepad"] = ("notepad.exe", null),
        ["paint"] = ("mspaint.exe", null),
        ["cmd"] = ("cmd.exe", null),
        ["command prompt"] = ("cmd.exe", null),
        ["powershell"] = ("powershell.exe", null),
        ["terminal"] = ("wt.exe", null),
        ["windows terminal"] = ("wt.exe", null),
        ["file explorer"] = ("explorer.exe", null),
        ["explorer"] = ("explorer.exe", null),
        ["registry editor"] = ("regedit.exe", null),
        ["regedit"] = ("regedit.exe", null),
        ["disk management"] = ("diskmgmt.msc", null),
        ["event viewer"] = ("eventvwr.msc", null),
        ["services"] = ("services.msc", null),
        ["system info"] = ("msinfo32.exe", null),
        ["resource monitor"] = ("resmon.exe", null),
        ["performance monitor"] = ("perfmon.exe", null),
        ["snipping tool"] = ("SnippingTool.exe", null),
        ["remote desktop"] = ("mstsc.exe", null),
    };

    public async Task<string> ExecuteAsync(JsonElement parameters)
    {
        var target = parameters.GetProperty("target").GetString();
        if (string.IsNullOrWhiteSpace(target))
            return JsonSerializer.Serialize(new { error = "Target is required" });

        var arguments = parameters.TryGetProperty("arguments", out var a) ? a.GetString() : null;
        var runAsAdmin = parameters.TryGetProperty("run_as_admin", out var r) && r.GetBoolean();
        var workingDir = parameters.TryGetProperty("working_directory", out var w) ? w.GetString() : null;

        return await Task.Run(() =>
        {
            try
            {
                // Check known apps first
                if (KnownApps.TryGetValue(target, out var known))
                {
                    target = known.Exe;
                    arguments ??= known.Args;
                }

                var psi = new ProcessStartInfo
                {
                    FileName = target,
                    UseShellExecute = true
                };

                if (!string.IsNullOrWhiteSpace(arguments))
                    psi.Arguments = arguments;

                if (!string.IsNullOrWhiteSpace(workingDir))
                    psi.WorkingDirectory = workingDir;

                if (runAsAdmin)
                    psi.Verb = "runas";

                var process = Process.Start(psi);

                return JsonSerializer.Serialize(new
                {
                    success = true,
                    message = $"Launched: {target}" + (arguments != null ? $" {arguments}" : ""),
                    pid = process?.Id
                });
            }
            catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "User cancelled the elevation prompt (UAC)"
                });
            }
            catch (Exception ex)
            {
                // If direct launch fails, try finding the app
                return TryFindAndLaunch(target, arguments, ex.Message);
            }
        });
    }

    private static string TryFindAndLaunch(string appName, string? arguments, string originalError)
    {
        try
        {
            // Try to find the app in App Paths registry
            using var key = Registry.LocalMachine.OpenSubKey(
                $@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\{appName}.exe");
            var path = key?.GetValue(null)?.ToString();

            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                var psi = new ProcessStartInfo
                {
                    FileName = path,
                    Arguments = arguments ?? "",
                    UseShellExecute = true
                };
                var process = Process.Start(psi);
                return JsonSerializer.Serialize(new
                {
                    success = true,
                    message = $"Launched: {path}",
                    pid = process?.Id
                });
            }
        }
        catch { }

        return JsonSerializer.Serialize(new
        {
            success = false,
            error = originalError,
            suggestion = $"Could not find '{appName}'. Try providing the full executable path, or use 'powershell' to locate it with: Get-Command {appName}"
        });
    }
}
