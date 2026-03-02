using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using TrueWindowsCopilot.Helpers;
using TrueWindowsCopilot.Services;
using TrueWindowsCopilot.Services.AI;
using TrueWindowsCopilot.Services.Windows;
using TrueWindowsCopilot.ViewModels;

namespace TrueWindowsCopilot;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    private Window? _window;

    public App()
    {
        this.InitializeComponent();

        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Helpers
        services.AddSingleton<SettingsHelper>();

        // HttpClient
        services.AddHttpClient("OpenAI");

        // AI Services
        services.AddSingleton<OpenAiChatService>();
        services.AddSingleton<ToolOrchestrator>();

        // Change log — tracks all system modifications for revert support
        services.AddSingleton<ChangeLogService>();

        // Windows Tools
        // powershell: read-only queries
        // system_change: mutations with revert scripts
        // revert_change / list_changes: undo support
        // launch_application: GUI operations via ShellExecute
        services.AddSingleton<IWindowsTool, PowerShellService>();
        services.AddSingleton<IWindowsTool, SystemChangeService>();
        services.AddSingleton<IWindowsTool, RevertChangeService>();
        services.AddSingleton<IWindowsTool, ListChangesService>();
        services.AddSingleton<IWindowsTool, AppLauncherService>();

        // ViewModels
        services.AddTransient<ChatViewModel>();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();
    }
}
