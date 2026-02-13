using System.Diagnostics;
using FileExplorer.Context;

namespace FileExplorer.Keybinds;

public class NemoKeybind(MenuContext context) : Keybind(context)
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
            
        proc.Start();
    }
}