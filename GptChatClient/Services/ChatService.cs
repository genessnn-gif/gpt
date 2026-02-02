using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using GptChatClient.Models;

namespace GptChatClient.Services;

public sealed class ChatService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly TimeSpan _stallTimeout;
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public ChatService(HttpClient httpClient, string apiKey, string model, TimeSpan stallTimeout)
    {
        _httpClient = httpClient;
        _apiKey = apiKey;
        _model = model;
        _stallTimeout = stallTimeout;
    }

    public async Task<string> GetResponseAsync(
        IReadOnlyList<ChatMessage> conversation,
        Func<string, Task> onBufferedChunk,
        CancellationToken cancellationToken)
    {
        using var streamingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var streamingToken = streamingCts.Token;
        var lastTokenAt = DateTimeOffset.UtcNow;
        var streamingInterrupted = false;
        var responseBuilder = new StringBuilder();

        var stallWatcher = Task.Run(async () =>
        {
            while (!streamingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250), streamingToken);
                if (DateTimeOffset.UtcNow - lastTokenAt > _stallTimeout)
                {
                    streamingInterrupted = true;
                    streamingCts.Cancel();
                }
            }
        }, streamingToken);

        try
        {
            await foreach (var chunk in StreamResponseAsync(conversation, streamingToken))
            {
                lastTokenAt = DateTimeOffset.UtcNow;
                responseBuilder.Append(chunk);
                await onBufferedChunk(chunk);
            }
        }
        catch (OperationCanceledException) when (streamingInterrupted)
        {
            // fall through to fetch full response
        }
        finally
        {
            streamingCts.Cancel();
            try
            {
                await stallWatcher;
            }
            catch (OperationCanceledException)
            {
            }
        }

        if (streamingInterrupted)
        {
            var fullResponse = await FetchCompletedResponseAsync(conversation, cancellationToken);
            return fullResponse;
        }

        return responseBuilder.ToString();
    }

    private async IAsyncEnumerable<string> StreamResponseAsync(
        IReadOnlyList<ChatMessage> conversation,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var request = BuildRequest(conversation, stream: true);
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (!line.StartsWith("data: ", StringComparison.Ordinal))
            {
                continue;
            }

            var payload = line[6..];
            if (payload == "[DONE]")
            {
                yield break;
            }

            using var document = JsonDocument.Parse(payload);
            if (!document.RootElement.TryGetProperty("choices", out var choices))
            {
                continue;
            }

            var delta = choices[0].GetProperty("delta");
            if (delta.TryGetProperty("content", out var contentElement))
            {
                var content = contentElement.GetString();
                if (!string.IsNullOrEmpty(content))
                {
                    yield return content;
                }
            }
        }
    }

    private async Task<string> FetchCompletedResponseAsync(
        IReadOnlyList<ChatMessage> conversation,
        CancellationToken cancellationToken)
    {
        using var request = BuildRequest(conversation, stream: false);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var content = document.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        return content ?? string.Empty;
    }

    private HttpRequestMessage BuildRequest(IReadOnlyList<ChatMessage> conversation, bool stream)
    {
        var payload = new
        {
            model = _model,
            stream,
            messages = conversation.Select(message => new
            {
                role = message.Role,
                content = message.Content
            })
        };

        var json = JsonSerializer.Serialize(payload, SerializerOptions);
        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        return request;
    }
}
