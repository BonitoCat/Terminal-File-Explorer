using System.Diagnostics;
using CmdMenu;
using CmdMenu.Controls;
using FileExplorer.Context;

namespace FileExplorer.FileTypes;

public static class DebFile
{
    public static void OnClick(MenuContext context, CmdLabel sender)
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