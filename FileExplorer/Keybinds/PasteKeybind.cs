namespace FileExplorer.Keybinds;

public class PasteKeybind(MenuContext context) : Keybind(context)
{
    public override void OnKeyDown(bool continuous)
    {
        lock (_context.Menu.Lock)
        {
            if (_context.MoveStyle == MoveStyle.Cut)
            {
                foreach (string itemPath in _context.MoveItems)
                {
                    try
                    {
                        string name = Path.GetFileName(itemPath);
                        if (Directory.Exists(itemPath))
                        {
                            Directory.Move(itemPath, name);
                            _context.SelectedItems.Add(_context.Menu.GetItemByText(name));
                        }
                        else if (File.Exists(itemPath))
                        {
                            File.Move(itemPath, name);
                            _context.SelectedItems.Add(_context.Menu.GetItemByText(name));
                        }
                    }
                    catch { }
                }
            }
            else if (_context.MoveStyle == MoveStyle.Copy)
            {
                foreach (string itemPath in _context.MoveItems)
                {
                    try
                    {
                        if (Directory.Exists(itemPath))
                        {
                            _context.CopyDirectory(itemPath, Path.GetFileName(itemPath));
                        }
                        else if (File.Exists(itemPath))
                        {
                            File.Copy(itemPath, Path.GetFileName(itemPath));
                        }
                    }
                    catch { }
                }
            }
            else
            {
                return;
            }
            
            _context.MoveStyle = MoveStyle.None;
            Console.Clear();
            
            Task.Run(() =>
            {
                _context.RefreshItems();
            });
        }
    }
}