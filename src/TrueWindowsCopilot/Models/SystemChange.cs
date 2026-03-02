namespace TrueWindowsCopilot.Models;

/// <summary>
/// Represents a system change made by the AI, with the ability to revert it.
/// </summary>
public class SystemChange
{
    public int Id { get; init; }
    public string Description { get; init; } = "";
    public string Script { get; init; } = "";
    public string RevertScript { get; init; } = "";
    public DateTime Timestamp { get; init; } = DateTime.Now;
    public bool IsReverted { get; set; }
    public string? Output { get; init; }
    public string? RevertOutput { get; set; }
}
