using FileExplorer.Tui.Context;

namespace FileExplorer.Tui.Keybinds;

public class CreateMenuKeybind(IEntryContext context, Action callback) : Keybind(context)
{
    public override void OnKeyUp()
    {
        callback.Invoke();
    }
}