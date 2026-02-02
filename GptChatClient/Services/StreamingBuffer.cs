using System.Text;
using System.Timers;
using System.Windows.Threading;

namespace GptChatClient.Services;

public sealed class StreamingBuffer : IDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly Action<string> _onFlush;
    private readonly Timer _timer;
    private readonly StringBuilder _buffer = new();
    private readonly object _lock = new();

    public StreamingBuffer(Dispatcher dispatcher, Action<string> onFlush, double intervalMs = 150)
    {
        _dispatcher = dispatcher;
        _onFlush = onFlush;
        _timer = new Timer(intervalMs) { AutoReset = true };
        _timer.Elapsed += (_, _) => Flush();
        _timer.Start();
    }

    public void Append(string chunk)
    {
        if (string.IsNullOrEmpty(chunk))
        {
            return;
        }

        lock (_lock)
        {
            _buffer.Append(chunk);
        }
    }

    public void Flush()
    {
        string? chunk = null;
        lock (_lock)
        {
            if (_buffer.Length == 0)
            {
                return;
            }

            chunk = _buffer.ToString();
            _buffer.Clear();
        }

        _ = _dispatcher.InvokeAsync(() => _onFlush(chunk));
    }

    public void Complete()
    {
        Flush();
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Dispose();
    }
}
