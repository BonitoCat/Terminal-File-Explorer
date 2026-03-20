using FileExplorer.Tui.Context;
using InputLib;

namespace FileExplorer.Tui.Options;

public class MenuContextOptions
{
    public object OutLock { get; set; }
    public ClipboardContext ClipboardContext { get; set; }
    public ManualResetEventSlim ExitEvent { get; set; }
    public bool ForceTtyInput { get; set; }
    
    public InputListener.KeyDownHandler HandleKeyDown { get; set; }
    public InputListener.KeyUpHandler HandleKeyUp { get; set; }
    public InputListener.KeyJustPressedHandler HandleKeyJustPressed { get; set; }
    public Action MenuUpdate { get; set; }
}