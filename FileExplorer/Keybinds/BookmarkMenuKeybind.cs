using System.Diagnostics;
using CmdMenu.Controls;

namespace FileExplorer.Keybinds;

public class BookmarkMenuKeybind(MenuContext context) : Keybind(context)
{
    public override void OnKeyJustPressed()
    {
        if (Directory.Exists(_context.BookmarkDir))
        {
            string cwd = Directory.GetCurrentDirectory();
            _context.OnClickDir(new(_context.BookmarkDir), cwd != _context.BookmarkDir);

            void ItemAdded(CmdListBoxItem<CmdLabel> item)
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