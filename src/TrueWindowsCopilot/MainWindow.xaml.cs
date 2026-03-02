using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using TrueWindowsCopilot.Helpers;
using TrueWindowsCopilot.ViewModels;
using Windows.System;

namespace TrueWindowsCopilot;

public sealed partial class MainWindow : Window
{
    public ChatViewModel ViewModel { get; }

    public MainWindow()
    {
        ViewModel = App.Services.GetRequiredService<ChatViewModel>();
        this.InitializeComponent();

        // Set window size
        var appWindow = this.AppWindow;
        appWindow.Resize(new Windows.Graphics.SizeInt32(960, 720));

        // Wire up confirmation dialog for irreversible operations
        ViewModel.ConfirmAction = ShowConfirmationDialogAsync;

        // Auto-scroll when new messages are added
        ViewModel.Messages.CollectionChanged += (_, _) =>
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (ViewModel.Messages.Count > 0)
                {
                    MessagesListView.ScrollIntoView(
                        ViewModel.Messages[^1],
                        ScrollIntoViewAlignment.Leading);
                }
            });
        };
    }

    private void OnInputKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            var shiftState = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift);
            bool isShiftDown = (shiftState & Windows.UI.Core.CoreVirtualKeyStates.Down) != 0;

            if (!isShiftDown)
            {
                if (ViewModel.SendCommand.CanExecute(null))
                {
                    ViewModel.SendCommand.Execute(null);
                }
                e.Handled = true;
            }
        }
    }

    private void OnSendClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SendCommand.CanExecute(null))
        {
            ViewModel.SendCommand.Execute(null);
        }
        InputTextBox.Focus(FocusState.Programmatic);
    }

    private void OnCopyMessageClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string text)
        {
            var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage();
            dataPackage.SetText(text);
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);
        }
    }

    private async void OnSettingsClick(object sender, RoutedEventArgs e)
    {
        var settings = App.Services.GetRequiredService<SettingsHelper>();

        var apiKeyBox = new PasswordBox
        {
            Header = "OpenAI API Key",
            PlaceholderText = "sk-...",
            Password = settings.ApiKey,
            Width = 400
        };

        var baseUrlBox = new TextBox
        {
            Header = "API Base URL",
            PlaceholderText = "https://api.openai.com/v1",
            Text = settings.ApiBaseUrl,
            Width = 400
        };

        var modelBox = new TextBox
        {
            Header = "Model Name",
            PlaceholderText = "gpt-5.2",
            Text = settings.ModelName,
            Width = 400
        };

        var panel = new StackPanel { Spacing = 16 };
        panel.Children.Add(apiKeyBox);
        panel.Children.Add(baseUrlBox);
        panel.Children.Add(modelBox);

        var dialog = new ContentDialog
        {
            Title = "Settings",
            Content = panel,
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.Content.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            settings.ApiKey = apiKeyBox.Password;
            settings.ApiBaseUrl = string.IsNullOrWhiteSpace(baseUrlBox.Text)
                ? "https://api.openai.com/v1"
                : baseUrlBox.Text.TrimEnd('/');
            settings.ModelName = string.IsNullOrWhiteSpace(modelBox.Text)
                ? "gpt-5.2"
                : modelBox.Text;
            settings.Save();
        }
    }

    private void OnNewChatClick(object sender, RoutedEventArgs e)
    {
        ViewModel.StartNewChat();
    }

    private async Task<bool> ShowConfirmationDialogAsync(string message)
    {
        var tcs = new TaskCompletionSource<bool>();

        DispatcherQueue.TryEnqueue(async () =>
        {
            var dialog = new ContentDialog
            {
                Title = "⚠️ Irreversible Operation",
                Content = new TextBlock
                {
                    Text = message,
                    TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap
                },
                PrimaryButtonText = "Proceed",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.Content.XamlRoot
            };

            var result = await dialog.ShowAsync();
            tcs.SetResult(result == ContentDialogResult.Primary);
        });

        return await tcs.Task;
    }
}
