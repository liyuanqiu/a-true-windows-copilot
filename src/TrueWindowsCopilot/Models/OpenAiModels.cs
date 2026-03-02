namespace TrueWindowsCopilot.Models;

// ── Request Models ────────────────────────────────────────────

public class ChatCompletionRequest
{
    public string Model { get; set; } = "";
    public List<ApiMessage> Messages { get; set; } = [];
    public List<ApiTool>? Tools { get; set; }
    public string? ToolChoice { get; set; }
    public double Temperature { get; set; } = 0.7;
}

public class ApiMessage
{
    public string Role { get; set; } = "";
    public string? Content { get; set; }
    public string? ToolCallId { get; set; }
    public List<ApiToolCall>? ToolCalls { get; set; }
}

public class ApiTool
{
    public string Type { get; set; } = "function";
    public ApiFunctionDefinition Function { get; set; } = new();
}

public class ApiFunctionDefinition
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public object? Parameters { get; set; }
}

public class ApiToolCall
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "function";
    public ApiFunctionCall Function { get; set; } = new();
}

public class ApiFunctionCall
{
    public string Name { get; set; } = "";
    public string Arguments { get; set; } = "";
}

// ── Response Models ───────────────────────────────────────────

public class ChatCompletionResponse
{
    public string? Id { get; set; }
    public List<ChatChoice> Choices { get; set; } = [];
    public UsageInfo? Usage { get; set; }
}

public class ChatChoice
{
    public int Index { get; set; }
    public ApiMessage Message { get; set; } = new();
    public string? FinishReason { get; set; }
}

public class UsageInfo
{
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
}
