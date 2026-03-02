using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using TrueWindowsCopilot.Helpers;
using TrueWindowsCopilot.Models;

namespace TrueWindowsCopilot.Services.AI;

public class OpenAiChatService
{
    private readonly IHttpClientFactory _httpClientFactory;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public OpenAiChatService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<ChatCompletionResponse> GetCompletionAsync(
        List<ApiMessage> messages,
        List<ApiTool> tools,
        SettingsHelper settings,
        CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient("OpenAI");

        var request = new ChatCompletionRequest
        {
            Model = settings.ModelName,
            Messages = messages,
            Tools = tools.Count > 0 ? tools : null,
            ToolChoice = tools.Count > 0 ? "auto" : null,
            Temperature = 0.7
        };

        var json = JsonSerializer.Serialize(request, JsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var baseUrl = settings.ApiBaseUrl.TrimEnd('/');
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/chat/completions")
        {
            Content = content
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);

        var response = await client.SendAsync(httpRequest, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException(
                $"OpenAI API returned {(int)response.StatusCode}: {errorBody}");
        }

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        var result = JsonSerializer.Deserialize<ChatCompletionResponse>(responseJson, JsonOptions);

        return result ?? throw new InvalidOperationException("Failed to deserialize API response.");
    }
}
