using CommunityToolkit.Mvvm.ComponentModel;

namespace TrueWindowsCopilot.Models;

public enum MessageRole
{
    System,
    User,
    Assistant,
    Tool
}

public partial class ChatMessage : ObservableObject
{
    public MessageRole Role { get; init; }

    [ObservableProperty]
    private string _content = string.Empty;
}
