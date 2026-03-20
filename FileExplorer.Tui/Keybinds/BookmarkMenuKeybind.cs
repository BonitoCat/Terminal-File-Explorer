using FileExplorer.Tui.Context;
using TuiLib.Controls;

namespace FileExplorer.Tui.Keybinds;

public class BookmarkMenuKeybind(IEntryContext context) : Keybind(context)
{
    public override void OnKeyJustPressed()
    {
        if (Directory.Exists(_context.BookmarkDir))
        {
            string cwd = Directory.GetCurrentDirectory();
            _context.OnClickDir(new(_context.BookmarkDir), cwd != _context.BookmarkDir);
            
            void ItemAdded(TuiListBoxItem<TuiLabel> item)
            {
                if (item.Item?.Text == "..")
                {
                    item.Data.TryAdd("DestinationPath", cwd);
                }
            }
            
            _context.Menu.OnItemAdded += ItemAdded;
            
            Task.Run(() =>
            {
                Task.Delay(20).Wait();
                _context.Menu.OnItemAdded -= ItemAdded;
            });
        }
    }
}