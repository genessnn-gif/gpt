# GPT Chat Client (Windows-native MVP)

A minimal WPF desktop client focused on long, reliable GPT conversations without UI freezing.

## Architecture (brief)
- **UI thread**: Renders only. WPF `MainWindow` binds to `ChatViewModel` and displays the latest 15 messages.
- **Network thread**: Uses `Task.Run` to handle streaming + recovery so the UI remains responsive.
- **Streaming with buffering**: Tokens are buffered and flushed to the UI every ~150ms via `StreamingBuffer`.
- **Reliability fallback**: If streaming stalls for 5 seconds, the client automatically fetches the full response and replaces the partial output.
- **Virtualized message list**: Only the most recent 15 messages are kept in the UI collection. Older messages are stored on disk.
- **Local persistence**: Conversations are appended to a JSONL file under `%LOCALAPPDATA%\GptChatClient\conversations.jsonl`.

## Build and run (Windows)

### Prerequisites
- Install the .NET 8 SDK for Windows: https://dotnet.microsoft.com/download
- Set your API key:
  - PowerShell: `$env:OPENAI_API_KEY = "your-key"`
  - Optional model override: `$env:OPENAI_MODEL = "gpt-4o-mini"`

### Build
```powershell
dotnet build .\GptChatClient\GptChatClient.csproj
```

### Run
```powershell
dotnet run --project .\GptChatClient\GptChatClient.csproj
```

## Notes
- This MVP uses the OpenAI Chat Completions endpoint with streaming.
- The UI always updates in buffered chunks rather than per-token.
- If streaming stalls, the full response is fetched and displayed to avoid missing answers.
