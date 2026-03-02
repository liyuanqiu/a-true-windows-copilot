using System.Text.Json;
using TrueWindowsCopilot.Models;

namespace TrueWindowsCopilot.Services.AI;

/// <summary>
/// Orchestrates tool registration, definition generation, and execution dispatch.
/// </summary>
public class ToolOrchestrator
{
    private readonly Dictionary<string, IWindowsTool> _tools;

    public ToolOrchestrator(IEnumerable<IWindowsTool> tools)
    {
        _tools = tools.ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Builds the tool definitions list for the OpenAI API request.
    /// </summary>
    public List<ApiTool> GetToolDefinitions()
    {
        return _tools.Values.Select(t => new ApiTool
        {
            Type = "function",
            Function = new ApiFunctionDefinition
            {
                Name = t.Name,
                Description = t.Description,
                Parameters = t.ParameterSchema
            }
        }).ToList();
    }

    /// <summary>
    /// Executes a tool by name with the given JSON arguments string.
    /// </summary>
    public async Task<string> ExecuteAsync(string toolName, string arguments)
    {
        if (!_tools.TryGetValue(toolName, out var tool))
        {
            return JsonSerializer.Serialize(new { error = $"Unknown tool: {toolName}" });
        }

        try
        {
            JsonElement parameters;
            if (string.IsNullOrWhiteSpace(arguments) || arguments == "{}")
            {
                parameters = JsonDocument.Parse("{}").RootElement;
            }
            else
            {
                parameters = JsonDocument.Parse(arguments).RootElement;
            }

            return await tool.ExecuteAsync(parameters);
        }
        catch (JsonException ex)
        {
            return JsonSerializer.Serialize(new { error = $"Invalid parameters JSON: {ex.Message}" });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = $"Tool execution failed: {ex.Message}" });
        }
    }

    /// <summary>
    /// Gets a tool by name, cast to a specific type.
    /// </summary>
    public T? GetTool<T>(string toolName) where T : class, IWindowsTool
    {
        return _tools.TryGetValue(toolName, out var tool) ? tool as T : null;
    }
}
