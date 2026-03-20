using System.Diagnostics;
using FileExplorer.Tui.Context;
using LoggerLib;

namespace FileExplorer.Tui.Keybinds;

public class NemoKeybind(IEntryContext context) : Keybind(context)
{
    public override void OnKeyUp()
    {
        Process proc = new();
        proc.StartInfo = new ProcessStartInfo
        {
            FileName = "nemo",
            Arguments = $"\"{Directory.GetCurrentDirectory()}\"",
            RedirectStandardOutput = true,
            RedirectStandardInput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        
        Logger.LogI("Opening external file explorer");
        proc.Start();
    }
}