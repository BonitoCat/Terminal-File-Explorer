using System.Diagnostics;
using CmdMenu;

namespace FileExplorer.FileTypes;

public static class DebFile
{
    public static void OnClick(MenuContext context, MenuItem sender)
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