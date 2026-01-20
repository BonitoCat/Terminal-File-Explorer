using FileLib;

namespace FileExplorer.Keybinds;

public class BookmarkMenuKeybind(MenuContext context) : Keybind(context)
{
    public override void OnKeyUp()
    {
        if (Directory.Exists(_context.BookmarkDir))
        {
            string cwd = Directory.GetCurrentDirectory();
            _context.OnClickDir(new(_context.BookmarkDir), cwd != _context.BookmarkDir);
            Task.Run(() =>
            {
                Task.Delay(20).Wait();
                _context.Menu.GetItemAt(0)?.Data.TryAdd("DestinationPath", cwd);
            });
        }
    }
}