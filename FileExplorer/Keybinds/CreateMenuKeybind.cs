namespace FileExplorer.Keybinds;

public class CreateMenuKeybind(MenuContext context, Action callback) : Keybind(context)
{
    public override void OnKeyUp()
    {
        callback.Invoke();
    }
}