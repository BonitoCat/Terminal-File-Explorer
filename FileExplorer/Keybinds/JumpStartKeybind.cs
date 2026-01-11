namespace FileExplorer.Keybinds;

public class JumpStartKeybind(MenuContext context) : Keybind(context)
{
    public override void OnKeyDown(bool continuous)
    {
        lock (_context.Menu.Lock)
        {
            _context.Menu.SelectedIndex = 0;
            _context.Menu.ViewIndex = 0;
        }
    }
}