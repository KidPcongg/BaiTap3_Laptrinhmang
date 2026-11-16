using System.Net.Sockets;
using UserMgmt.Shared;

namespace UserMgmt.Client;

public sealed class ServerConnection : IDisposable
{
    private TcpClient? _tcp;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private TaskCompletionSource<Response>? _pending;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private Task? _receiveLoop;
    private bool _expectClose;

    public event Action<string>? StatusChanged;
    public event Action? Disconnected;

    public bool IsConnected => _tcp is not null && _receiveLoop is { IsCompleted: false };

    public async Task ConnectAsync(string host, int port)
    {
        Dispose();
        _expectClose = false;
        _tcp = new TcpClient();
        await _tcp.ConnectAsync(host, port).WaitAsync(TimeSpan.FromSeconds(5));
        NetworkStream stream = _tcp.GetStream();
        _reader = Framing.CreateReader(stream);
        _writer = Framing.CreateWriter(stream);
        StatusChanged?.Invoke($"Đã kết nối {host}:{port}");
        _receiveLoop = ReceiveLoopAsync(_tcp, _reader);
    }

    public async Task<Response> SendAsync(Request request)
    {
        if (_writer is null || !IsConnected)
            throw new IOException("Chưa kết nối tới server.");

        await _sendLock.WaitAsync();
        try
        {
            _pending = new TaskCompletionSource<Response>(TaskCreationOptions.RunContinuationsAsynchronously);
            await Framing.WriteAsync(_writer, request);
            return await _pending.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally
        {
            _pending = null;
            _sendLock.Release();
        }
    }

    private async Task ReceiveLoopAsync(TcpClient tcp, StreamReader reader)
    {
        try
        {
            while (true)
            {
                Response? response = await Framing.ReadAsync<Response>(reader);
                if (response is null)
                    break;
                _pending?.TrySetResult(response);
            }
        }
        catch (IOException) { }
        catch (SocketException) { }
        catch (ObjectDisposedException) { }
        catch (System.Text.Json.JsonException) { }

        bool closedByUser = _expectClose || !ReferenceEquals(tcp, _tcp);
        if (!closedByUser)
            _pending?.TrySetException(new IOException("Mất kết nối tới server."));

        StatusChanged?.Invoke(closedByUser ? "Đã ngắt kết nối" : "Mất kết nối tới server");
        Disconnected?.Invoke();
    }

    public async Task LogoutAndCloseAsync()
    {
        _expectClose = true;
        try
        {
            if (IsConnected)
                await SendAsync(new Request(Commands.DangXuat));
        }
        catch (Exception ex) when (ex is IOException or SocketException or TimeoutException) { }
        finally
        {
            Dispose();
        }
    }

    public void Dispose()
    {
        _expectClose = true;
        _tcp?.Close();
        _tcp = null;
        _reader = null;
        _writer = null;
    }
}
