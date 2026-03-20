namespace FileExplorer.Tui.RemotePaths;

// Manages active remote connections and exposes them to the UI.
// One connection per pane — the second menu pane gets a RemoteMenuContext
// instead of a local one.
public class RemoteConnectionManager : IDisposable
{
    private readonly Dictionary<string, IRemoteConnection> _connections = new();

    public IReadOnlyDictionary<string, IRemoteConnection> Connections => _connections;

    public async Task<IRemoteConnection> ConnectAsync(RemoteConnectionInfo info)
    {
        string key = ConnectionKey(info);

        if (_connections.TryGetValue(key, out IRemoteConnection? existing))
        {
            if (existing.IsConnected)
            {
                return existing;
            }

            existing.Dispose();
            _connections.Remove(key);
        }

        IRemoteConnection conn = IRemoteConnection.Create(info);
        await conn.ConnectAsync();
        _connections[key] = conn;

        return conn;
    }

    public async Task DisconnectAsync(string key)
    {
        if (!_connections.TryGetValue(key, out IRemoteConnection? conn))
            return;

        await conn.DisconnectAsync();
        conn.Dispose();
        _connections.Remove(key);
    }

    public IRemoteConnection? Get(string key) =>
        _connections.TryGetValue(key, out IRemoteConnection? conn) ? conn : null;

    public bool IsConnected(string key) =>
        _connections.TryGetValue(key, out IRemoteConnection? conn) && conn.IsConnected;

    public void Dispose()
    {
        foreach (IRemoteConnection conn in _connections.Values)
        {
            try { conn.Dispose(); } catch { }
        }

        _connections.Clear();
    }

    public static string ConnectionKey(RemoteConnectionInfo info) =>
        $"{info.Protocol}://{info.Username}@{info.Host}:{info.Port}";
}
