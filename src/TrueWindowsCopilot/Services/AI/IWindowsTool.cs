using System.Text.Json;

namespace TrueWindowsCopilot.Services.AI;

/// <summary>
/// Interface for all Windows tools that the AI can invoke via function calling.
/// </summary>
public interface IWindowsTool
{
    /// <summary>
    /// The unique name of this tool (used in OpenAI function calling).
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Human-readable description of what this tool does.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// JSON Schema object describing the tool's parameters.
    /// Will be serialized as-is into the OpenAI tools array.
    /// </summary>
    object ParameterSchema { get; }

    /// <summary>
    /// Execute the tool with the given parameters.
    /// </summary>
    /// <param name="parameters">JSON parameters from the AI.</param>
    /// <returns>A JSON string with the tool's result.</returns>
    Task<string> ExecuteAsync(JsonElement parameters);
}
