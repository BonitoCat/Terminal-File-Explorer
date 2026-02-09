using InputLib.EventArgs;

namespace FileExplorer.Keybinds;

public class SwitchMenuKeybind(MenuContext context, int dir, Action<int> callback) : Keybind(context)
{
    public override void OnKeyDown(KeyDownEventArgs e)
    {
        callback.Invoke(dir);
    }
}