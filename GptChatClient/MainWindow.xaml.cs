using System.Net.Http;
using System.Windows;
using GptChatClient.Models;
using GptChatClient.Services;
using GptChatClient.ViewModels;

namespace GptChatClient;

public partial class MainWindow : Window
{
    private const int MaxVisibleMessages = 15;
    private static readonly TimeSpan StallTimeout = TimeSpan.FromSeconds(5);
    private readonly ConversationStore _store;
    private readonly ChatService _chatService;
    private readonly ChatViewModel _viewModel;
    private CancellationTokenSource? _currentRequestCts;

    public MainWindow()
    {
        InitializeComponent();

        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? string.Empty;
        var model = Environment.GetEnvironmentVariable("OPENAI_MODEL") ?? "gpt-4o-mini";
        var dataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GptChatClient",
            "conversations.jsonl");

        _store = new ConversationStore(dataPath);
        _chatService = new ChatService(new HttpClient(), apiKey, model, StallTimeout);
        _viewModel = new ChatViewModel(SendAsync, MaxVisibleMessages);
        DataContext = _viewModel;

        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        var recent = await _store.ReadLastAsync(MaxVisibleMessages, CancellationToken.None);
        _viewModel.LoadMessages(recent);
    }

    private async Task SendAsync()
    {
        var prompt = _viewModel.InputText.Trim();
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return;
        }

        _currentRequestCts?.Cancel();
        _currentRequestCts = new CancellationTokenSource();
        var cancellationToken = _currentRequestCts.Token;

        _viewModel.InputText = string.Empty;

        var userMessage = new ChatMessage("user", prompt, DateTimeOffset.UtcNow);
        await _store.AppendAsync(userMessage, cancellationToken);
        _viewModel.AddMessage(userMessage);

        var assistantViewModel = _viewModel.AddAssistantPlaceholder();

        var buffered = new StreamingBuffer(Dispatcher, assistantViewModel.Append, 150);
        string finalResponse;

        try
        {
            finalResponse = await Task.Run(async () =>
            {
                var conversation = await _store.ReadAllAsync(cancellationToken);
                return await _chatService.GetResponseAsync(conversation, chunk =>
                {
                    buffered.Append(chunk);
                    return Task.CompletedTask;
                }, cancellationToken);
            }, cancellationToken);
        }
        finally
        {
            buffered.Complete();
            buffered.Dispose();
        }

        await Dispatcher.InvokeAsync(() => assistantViewModel.Content = finalResponse);

        var assistantMessage = new ChatMessage("assistant", finalResponse, DateTimeOffset.UtcNow);
        await _store.AppendAsync(assistantMessage, cancellationToken);
    }
}
