using System.Diagnostics;
using CmdMenu;
using CmdMenu.Controls;
using FileExplorer.Context;

namespace FileExplorer.FileTypes;

public static class ExecutableFile
{
    public static void OnClick(MenuContext context, CmdLabel sender)
    {
        Process proc = new()
        {
            StartInfo =
            {
                FileName = "gnome-terminal",
                Arguments = $"-- bash -c '{Path.GetFullPath(sender.Text)}'; exec bash",
                UseShellExecute = false,
            },
        };

        proc.Start();
        proc.WaitForExit();
    }
    
    public static bool IsExecutable(string path)
    {
        Process process = new()
        {
            StartInfo = new()
            {
                FileName = "bash",
                Arguments = $"-c \"[ -x \\\"{path}\\\" ]\"",
                RedirectStandardOutput = false,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };
        
        process.Start();
        process.WaitForExit();
        
        return process.ExitCode == 0;
    }
}