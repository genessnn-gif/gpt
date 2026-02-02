using System.Text.Json;
using GptChatClient.Models;

namespace GptChatClient.Services;

public sealed class ConversationStore
{
    private readonly string _filePath;
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public ConversationStore(string filePath)
    {
        _filePath = filePath;
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
    }

    public async Task AppendAsync(ChatMessage message, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(message, SerializerOptions);
        await File.AppendAllTextAsync(_filePath, json + Environment.NewLine, cancellationToken);
    }

    public async Task<IReadOnlyList<ChatMessage>> ReadAllAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath))
        {
            return Array.Empty<ChatMessage>();
        }

        var lines = await File.ReadAllLinesAsync(_filePath, cancellationToken);
        var messages = new List<ChatMessage>(lines.Length);
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var message = JsonSerializer.Deserialize<ChatMessage>(line, SerializerOptions);
            if (message is not null)
            {
                messages.Add(message);
            }
        }

        return messages;
    }

    public async Task<IReadOnlyList<ChatMessage>> ReadLastAsync(int count, CancellationToken cancellationToken)
    {
        var messages = await ReadAllAsync(cancellationToken);
        return messages.TakeLast(count).ToList();
    }
}
