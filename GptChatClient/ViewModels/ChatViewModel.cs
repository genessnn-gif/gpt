using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using GptChatClient.Models;

namespace GptChatClient.ViewModels;

public sealed class ChatViewModel : INotifyPropertyChanged
{
    private readonly int _maxVisible;
    private string _inputText = string.Empty;

    public ChatViewModel(Func<Task> sendAsync, int maxVisible)
    {
        _maxVisible = maxVisible;
        SendCommand = new AsyncRelayCommand(sendAsync, () => !string.IsNullOrWhiteSpace(InputText));
    }

    public ObservableCollection<MessageViewModel> Messages { get; } = new();

    public AsyncRelayCommand SendCommand { get; }

    public string InputText
    {
        get => _inputText;
        set
        {
            if (_inputText == value)
            {
                return;
            }

            _inputText = value;
            OnPropertyChanged();
            SendCommand.RaiseCanExecuteChanged();
        }
    }

    public void LoadMessages(IEnumerable<ChatMessage> messages)
    {
        Messages.Clear();
        foreach (var message in messages)
        {
            Messages.Add(MessageViewModel.FromMessage(message));
        }
    }

    public MessageViewModel AddMessage(ChatMessage message)
    {
        var viewModel = MessageViewModel.FromMessage(message);
        Messages.Add(viewModel);
        TrimMessages();
        return viewModel;
    }

    public MessageViewModel AddAssistantPlaceholder()
    {
        var placeholder = new MessageViewModel("assistant", string.Empty);
        Messages.Add(placeholder);
        TrimMessages();
        return placeholder;
    }

    public void ReplaceMessage(MessageViewModel target, ChatMessage message)
    {
        target.Content = message.Content;
    }

    private void TrimMessages()
    {
        while (Messages.Count > _maxVisible)
        {
            Messages.RemoveAt(0);
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
