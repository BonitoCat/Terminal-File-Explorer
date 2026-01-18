using InputLib.EventArgs;
using Tmds.DBus;

namespace FileExplorer.Keybinds;

[DBusInterface("org.freedesktop.portal.Clipboard")]
interface IClipboard : IDBusObject
{
    Task SetSelectionAsync(
        ObjectPath session_handle,
        IDictionary<string, object> options,
        IDictionary<string, object> data);
}

[DBusInterface("org.freedesktop.portal.Session")]
interface ISession : IDBusObject
{
    Task CloseAsync();
}

public class SavePathKeybind(MenuContext context) : Keybind(context)
{
    public override async void OnKeyDown(KeyDownEventArgs e)
    {
        await CopyToClipboardAsync(Directory.GetCurrentDirectory());
    }
    
    static async Task CopyToClipboardAsync(string text)
    {
        Connection conn = new(Address.Session);
        await conn.ConnectAsync();

        IClipboard clipboard = conn.CreateProxy<IClipboard>(
            "org.freedesktop.portal.Desktop",
            "/org/freedesktop/portal/desktop");

        ObjectPath sessionPath = new("/org/freedesktop/portal/desktop/session/" + Guid.NewGuid().ToString("N"));
        var session = conn.CreateProxy<ISession>("org.freedesktop.portal.Desktop", sessionPath);

        Dictionary<string, object> data = new()
        {
            ["text/plain;charset=utf-8"] = text,
        };

        await clipboard.SetSelectionAsync(
            sessionPath,
            new Dictionary<string, object>(),
            data);
    }
}