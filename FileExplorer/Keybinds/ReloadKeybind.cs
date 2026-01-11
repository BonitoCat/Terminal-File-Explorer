namespace FileExplorer.Keybinds;

public class ReloadKeybind(MenuContext context) : Keybind(context)
{
    public override void OnKeyDown(bool continuous)
    {
        Task.Run(() =>
        {
            Console.Clear();
            _context.RefreshItems();
        });
    }
}