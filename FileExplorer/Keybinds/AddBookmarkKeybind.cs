using TuiLib;
using FileExplorer.Context;
using LoggerLib;

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
            Logger.LogI("Removed directory from bookmarks");
        }
        else
        {
            Directory.CreateSymbolicLink(Path.Combine(_context.BookmarkDir, dirName), cwd);
            Logger.LogI("Added directory to bookmarks");
        }
    }
}