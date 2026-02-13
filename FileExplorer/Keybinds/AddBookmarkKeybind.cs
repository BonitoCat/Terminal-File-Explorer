using CmdMenu;
using FileExplorer.Context;

namespace FileExplorer.Keybinds;

public class AddBookmarkKeybind(MenuContext context) : Keybind(context)
{
    public override void OnKeyUp()
    {
        string cwd = Directory.GetCurrentDirectory();
        if (cwd == _context.BookmarkDir)
        {
            return;
        }

        string dirName = Path.GetFileName(cwd);
        if (Directory.Exists(Path.Combine(_context.BookmarkDir, dirName)))
        {
            Directory.Delete(Path.Combine(_context.BookmarkDir, dirName));
        }
        else
        {
            Directory.CreateSymbolicLink(Path.Combine(_context.BookmarkDir, dirName), cwd);
        }
    }
}