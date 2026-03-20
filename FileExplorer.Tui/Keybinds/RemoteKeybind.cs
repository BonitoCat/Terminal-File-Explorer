using FileExplorer.Tui.Context;
using FileExplorer.Tui.Options;
using FileExplorer.Tui.RemotePaths;
using InputLib.EventArgs;

namespace FileExplorer.Tui.Keybinds;

public class RemoteKeybind(
    IEntryContext context,
    List<IEntryContext> contexts,
    int contextIndex,
    MenuContextOptions _contextOptions,
    RemoteConnectionManager manager,
    Action updateContexts) : Keybind(context)
{
    public override void OnKeyDown(KeyDownEventArgs e)
    {
        string? protocolStr = _context.Input("Protocol (sftp/ftp/smb): ");
        if (string.IsNullOrWhiteSpace(protocolStr)) return;

        if (!Enum.TryParse(protocolStr.Trim(), ignoreCase: true, out RemoteProtocol protocol))
        {
            return;
        }

        string? host = _context.Input("Host: ");
        if (string.IsNullOrWhiteSpace(host)) return;

        int defaultPort = protocol switch
        {
            RemoteProtocol.Sftp => 22,
            RemoteProtocol.Ftp => 21,
            RemoteProtocol.Smb => 445,
            _ => 22,
        };
        
        string? portStr = _context.Input($"Port [{defaultPort}]: ", defaultPort.ToString());
        int port = int.TryParse(portStr, out int p) ? p : defaultPort;

        string? username = _context.Input("Username: ");
        if (string.IsNullOrWhiteSpace(username)) return;

        string? password = _context.Input("Password: ", inputHidden: true);

        RemoteConnectionInfo connInfo = new(protocol, host.Trim(), port, username.Trim(), password);

        _context.Listener.PauseListening = true;

        if (contexts.Count == 2)
        {
            contexts.RemoveAt((contextIndex + 1) % 2);
        }

        Task.Run(async () =>
        {
            try
            {
                IRemoteConnection conn = await manager.ConnectAsync(connInfo);
                RemoteEntryContext remoteCtx = new(conn, _contextOptions);
                
                contexts.Add(remoteCtx);
                updateContexts();
                
                await remoteCtx.NavigateAsync("/");
            }
            catch (Exception ex)
            {
                /*targetContext.Menu.ClearItems();
                targetContext.Menu.AddItem(new TuiListBoxItem<TuiLabel>(new TuiLabel($"Connection failed: {ex.Message}")));
                targetContext.RedrawMenu();*/
            }
            finally
            {
                _context.Listener.PauseListening = false;
            }
        });
    }
}
