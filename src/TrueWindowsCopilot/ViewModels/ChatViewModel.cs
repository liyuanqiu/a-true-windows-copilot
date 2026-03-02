using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using TrueWindowsCopilot.Helpers;
using TrueWindowsCopilot.Models;
using TrueWindowsCopilot.Services.AI;
using TrueWindowsCopilot.Services.Windows;

namespace TrueWindowsCopilot.ViewModels;

public partial class ChatViewModel : ObservableObject
{
    private readonly OpenAiChatService _openAiService;
    private readonly ToolOrchestrator _toolOrchestrator;
    private readonly SettingsHelper _settings;
    private List<ApiMessage> _conversationHistory = [];

    public ObservableCollection<ChatMessage> Messages { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSend))]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    private string _inputText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BusyVisibility))]
    [NotifyPropertyChangedFor(nameof(CanSend))]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string _busyText = "Thinking...";

    public bool CanSend => !IsBusy && !string.IsNullOrWhiteSpace(InputText);

    public Visibility BusyVisibility => IsBusy ? Visibility.Visible : Visibility.Collapsed;

    private static readonly string SystemPrompt = """
        You are "A True Windows Copilot" — an autonomous AI agent running locally on a Windows PC.
        You have full access to PowerShell and can perform ANY system operation by writing and executing scripts.

        ## Your Tools

        1. `powershell` — Execute PowerShell scripts for **read-only** operations (queries, lookups, info gathering).
        2. `system_change` — Execute PowerShell scripts that **modify** the system. You MUST provide a revert script so the change can be undone.
        3. `revert_change` — Revert a previously made system change by its ID.
        4. `list_changes` — List all system changes made in this session with their revert status.
        5. `launch_application` — Open GUI apps, URLs, or system tools (uses ShellExecute).

        ## Critical Rule: Read vs. Write

        - **Reading** (listing files, getting system info, querying processes, checking settings) → use `powershell`
        - **Writing** (killing processes, changing settings, modifying files, editing registry, managing services, deleting anything) → use `system_change` with a revert script

        This distinction is essential. Every system modification MUST go through `system_change` so the user can undo it.

        ## How to Write Revert Scripts

        When using `system_change`, you must provide a `revert_script` that undoes the change:
        - **Before modifying a setting**, first query its current value with `powershell`, then use that value in the revert script.
        - **Example**: To switch to dark mode, first read the current theme, then set revert_script to restore it.
        - **Example**: To kill a process, record the executable path so revert_script can restart it.

        ## Irreversible Operations

        For operations that CANNOT be undone (e.g., uninstalling software, emptying recycle bin, permanently deleting files),
        you MUST set `irreversible=true` in the `system_change` call. This triggers a confirmation dialog — the user must
        explicitly approve before the operation executes. Set `revert_script` to an empty string for these operations.

        ## How to Operate

        You are NOT limited to a pre-defined set of operations. You can do ANYTHING that PowerShell can do.
        Think like a senior Windows sysadmin — figure out the right commands yourself.

        Examples of what you can do (not exhaustive):
        - List installed apps: query the Registry or use Get-AppxPackage
        - System info: Get-CimInstance, systeminfo, Get-ComputerInfo
        - Processes: Get-Process, Stop-Process
        - Services: Get-Service, Start-Service, Stop-Service
        - Files: Get-ChildItem, Copy-Item, Move-Item, Remove-Item
        - Network: Get-NetAdapter, Test-NetConnection, ipconfig
        - Settings: Registry edits, Set-ItemProperty
        - Disk: Get-Volume, Get-Disk, Get-PSDrive
        - Users: Get-LocalUser, whoami
        - Environment: [System.Environment] class, $env:
        - Scheduled tasks: Get-ScheduledTask
        - Event logs: Get-EventLog, Get-WinEvent
        - Firewall: Get-NetFirewallRule
        - Clipboard: Get-Clipboard, Set-Clipboard
        - ANY other PowerShell cmdlet, .NET API, WMI query, or COM object

        ## Guidelines

        1. **Never guess** — always run a command to get real data.
        2. **Be autonomous and proactive** — figure out the right approach yourself. Extract keywords, infer context, and try searches on your own. Do NOT ask the user to provide keywords or narrow down when you can extract them from the conversation yourself.
        3. **Explain destructive operations** before executing.
        4. **Always use `system_change` for mutations** — never use `powershell` for write operations.
        5. **Query before modifying** — read the current state first so you can write an accurate revert script.
        6. **Use ConvertTo-Json** when you need structured output from PowerShell.
        7. **Chain multiple calls** for complex tasks — gather info first, then act. If a filename search returns nothing, try a content search. If one set of keywords doesn't work, try different ones.
        8. **Format responses** clearly with bullet points, tables, or code blocks.
        9. **If a command fails**, read the error, adapt, and retry with a different approach.
        10. When the user asks to undo/revert, use `list_changes` to find the change and `revert_change` to undo it.
        11. **For file search**: extract likely keywords from the user's description and search immediately. For example, if the user says "a design spec for an AI-powered memory framework", search for keywords like "memory", "framework", "design", "spec", "AI" — don't ask the user to provide them.
        """;

    public ChatViewModel(OpenAiChatService openAiService, ToolOrchestrator toolOrchestrator, SettingsHelper settings)
    {
        _openAiService = openAiService;
        _toolOrchestrator = toolOrchestrator;
        _settings = settings;

        StartNewChat();
    }

    /// <summary>
    /// Callback to ask the user for confirmation (set by MainWindow).
    /// Takes a description string, returns true if user confirms.
    /// </summary>
    public Func<string, Task<bool>>? ConfirmAction { get; set; }

    public void StartNewChat()
    {
        Messages.Clear();
        _conversationHistory =
        [
            new ApiMessage { Role = "system", Content = SystemPrompt }
        ];

        var welcomeMessage = string.IsNullOrEmpty(_settings.ApiKey)
            ? """
              Welcome! Please configure your OpenAI API key in Settings (⚙️ top-right) to get started.

              I'm your True Windows Copilot — an autonomous AI agent that can do anything on your Windows system via PowerShell.
              """
            : """
              Hello! I'm your **True Windows Copilot** — an autonomous AI agent with full PowerShell access.

              I can figure out how to do **anything** on your Windows system. Just ask in plain language:

              - "List all my installed applications"
              - "What's using the most CPU right now?"
              - "Show me my system specs"
              - "Find large files on my C: drive"
              - "Switch to dark mode"
              - "What's my Wi-Fi password?"
              - "Check if any Windows updates are pending"
              """;

        Messages.Add(new ChatMessage
        {
            Role = MessageRole.Assistant,
            Content = welcomeMessage
        });
    }

    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendAsync()
    {
        if (string.IsNullOrWhiteSpace(InputText)) return;

        var userText = InputText.Trim();
        InputText = string.Empty;

        // Show user message
        Messages.Add(new ChatMessage { Role = MessageRole.User, Content = userText });
        _conversationHistory.Add(new ApiMessage { Role = "user", Content = userText });

        IsBusy = true;
        BusyText = "Thinking...";

        try
        {
            if (string.IsNullOrEmpty(_settings.ApiKey))
            {
                Messages.Add(new ChatMessage
                {
                    Role = MessageRole.Assistant,
                    Content = "⚠️ No API key configured. Please open Settings (⚙️) and enter your OpenAI API key."
                });
                return;
            }

            var tools = _toolOrchestrator.GetToolDefinitions();

            // Conversation loop: keep calling until we get a text response (not tool calls)
            const int maxRounds = 10;
            for (int round = 0; round < maxRounds; round++)
            {
                var response = await _openAiService.GetCompletionAsync(
                    _conversationHistory, tools, _settings);

                var choice = response.Choices.FirstOrDefault();
                if (choice == null)
                {
                    Messages.Add(new ChatMessage
                    {
                        Role = MessageRole.Assistant,
                        Content = "⚠️ Received empty response from AI."
                    });
                    return;
                }

                var assistantMsg = choice.Message;

                // If there are tool calls, execute them
                if (assistantMsg.ToolCalls is { Count: > 0 })
                {
                    // Add the assistant message with tool calls to history
                    _conversationHistory.Add(new ApiMessage
                    {
                        Role = "assistant",
                        Content = assistantMsg.Content,
                        ToolCalls = assistantMsg.ToolCalls
                    });

                    foreach (var toolCall in assistantMsg.ToolCalls)
                    {
                        var toolName = toolCall.Function.Name;
                        BusyText = $"Running {toolName}...";

                        // Show tool execution in UI
                        var toolMessage = new ChatMessage
                        {
                            Role = MessageRole.Tool,
                            Content = $"⚡ Executing: {toolName}"
                        };
                        Messages.Add(toolMessage);

                        // Execute tool
                        var toolResult = await _toolOrchestrator.ExecuteAsync(
                            toolName, toolCall.Function.Arguments);

                        // Check if this is an irreversible operation requiring confirmation
                        if (toolName == "system_change" && toolResult.Contains("requires_confirmation"))
                        {
                            var systemChangeSvc = _toolOrchestrator.GetTool<SystemChangeService>("system_change");
                            var pending = systemChangeSvc?.PendingChange;
                            if (pending != null)
                            {
                                var confirmed = ConfirmAction != null
                                    && await ConfirmAction($"⚠️ Irreversible operation:\n\n{pending.Description}\n\nThis cannot be undone. Proceed?");

                                if (confirmed)
                                {
                                    toolResult = await systemChangeSvc!.ExecuteChange(
                                        pending.Description, pending.Script, pending.RevertScript, pending.TimeoutSeconds);
                                    toolMessage.Content = $"✔ {toolName} completed (user confirmed)";
                                }
                                else
                                {
                                    toolResult = """{"cancelled": true, "message": "User declined the irreversible operation."}""";
                                    toolMessage.Content = $"✖ {toolName} cancelled by user";
                                }
                                systemChangeSvc!.PendingChange = null;
                            }
                        }
                        else
                        {
                            // Update tool message for normal execution
                            toolMessage.Content = $"✔ {toolName} completed";
                        }

                        // Add tool result to history
                        _conversationHistory.Add(new ApiMessage
                        {
                            Role = "tool",
                            Content = toolResult,
                            ToolCallId = toolCall.Id
                        });
                    }

                    BusyText = "Thinking...";
                    continue; // Loop again to get the text response
                }

                // Text response — we're done
                var responseText = assistantMsg.Content ?? "(no response)";

                Messages.Add(new ChatMessage
                {
                    Role = MessageRole.Assistant,
                    Content = responseText
                });

                _conversationHistory.Add(new ApiMessage
                {
                    Role = "assistant",
                    Content = responseText
                });

                return;
            }

            // If we exhausted max rounds
            Messages.Add(new ChatMessage
            {
                Role = MessageRole.Assistant,
                Content = "⚠️ Reached maximum tool execution rounds. Please try a simpler request."
            });
        }
        catch (HttpRequestException ex)
        {
            Messages.Add(new ChatMessage
            {
                Role = MessageRole.Assistant,
                Content = $"⚠️ Network error: {ex.Message}\n\nPlease check your API key and network connection in Settings."
            });
        }
        catch (Exception ex)
        {
            Messages.Add(new ChatMessage
            {
                Role = MessageRole.Assistant,
                Content = $"⚠️ Error: {ex.Message}"
            });
        }
        finally
        {
            IsBusy = false;
        }
    }
}
