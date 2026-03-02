using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TrueWindowsCopilot.Models;

namespace TrueWindowsCopilot.Converters;

public class MessageTemplateSelector : DataTemplateSelector
{
    public DataTemplate? UserTemplate { get; set; }
    public DataTemplate? AssistantTemplate { get; set; }
    public DataTemplate? ToolTemplate { get; set; }

    protected override DataTemplate SelectTemplateCore(object item)
    {
        if (item is ChatMessage message)
        {
            return message.Role switch
            {
                MessageRole.User => UserTemplate!,
                MessageRole.Tool => ToolTemplate!,
                _ => AssistantTemplate!,
            };
        }
        return AssistantTemplate!;
    }

    protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
    {
        return SelectTemplateCore(item);
    }
}
