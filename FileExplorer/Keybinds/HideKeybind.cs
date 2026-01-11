namespace FileExplorer.Keybinds;

public class HideKeybind(MenuContext context) : Keybind(context)
{
    public override void OnKeyDown(bool continuous)
    {
        lock (_context.Menu.Lock)
        {
            _context.ShowHiddenFiles = !_context.ShowHiddenFiles;
            Task.Run(() =>
            {
                Console.Clear();
                
                _context.Menu.ClearItems();
                _context.RefreshItems();
            });
        }
    }
}