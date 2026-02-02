using System.ComponentModel;
using System.Runtime.CompilerServices;
using GptChatClient.Models;

namespace GptChatClient.ViewModels;

public sealed class MessageViewModel : INotifyPropertyChanged
{
    private string _content;

    public MessageViewModel(string role, string content)
    {
        Role = role;
        _content = content;
    }

    public string Role { get; }

    public string Content
    {
        get => _content;
        set
        {
            if (_content == value)
            {
                return;
            }

            _content = value;
            OnPropertyChanged();
        }
    }

    public void Append(string chunk)
    {
        if (string.IsNullOrEmpty(chunk))
        {
            return;
        }

        Content += chunk;
    }

    public static MessageViewModel FromMessage(ChatMessage message) => new(message.Role, message.Content);

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
