using System.Diagnostics;
using FileExplorer.Tui.Context;
using TuiLib.Controls;

namespace FileExplorer.Tui.FileTypes;

public static class DebFile
{
    public static void OnClick(IEntryContext context, TuiLabel sender)
    {
        Process proc = new()
        {
            StartInfo =
            {
                FileName = "captain",
                Arguments = $"\"{sender.Text}\"",
                UseShellExecute = false,
            },
        };

        proc.Start();
    }

    public static bool IsDeb(string path)
    {
        return Path.GetExtension(path) == ".deb";
    }
}