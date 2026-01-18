using InputLib.EventArgs;

namespace FileExplorer.Keybinds;

public class ReloadKeybind(MenuContext context) : Keybind(context)
{
    public override void OnKeyDown(KeyDownEventArgs e)
    {
        Console.Clear();
        _context.RefreshItems();
    }
}