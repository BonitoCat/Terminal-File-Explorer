using System.Diagnostics;
using System.Runtime.InteropServices;
using FileExplorer.Tui.Context;
using TuiLib.Controls;

namespace FileExplorer.Tui.FileTypes;

public static class ExecutableFile
{
    [DllImport("libc", SetLastError = true)]
    private static extern int access(string pathname, int mode);

    private const int X_OK = 1;
    
    public static void OnClickTerm(IEntryContext context, TuiLabel sender)
    {
        string filePath = Path.GetFullPath(sender.Text);
    
        ProcessStartInfo startInfo = GetTerminalStartInfo(filePath);
        Process proc = new() { StartInfo = startInfo };
        
        proc.Start();
        proc.WaitForExit();
    }
    
    public static void OnClickDefault(IEntryContext context, TuiLabel sender)
    {
        string filePath = Path.GetFullPath(sender.Text);
        Process proc = new()
        {
            StartInfo = new()
            {
                FileName = filePath,
                UseShellExecute = false,
            },
        };
        
        proc.Start();
        proc.WaitForExit();
    }
    
    private static ProcessStartInfo GetTerminalStartInfo(string filePath)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/k \"{filePath}\"",
                UseShellExecute = false,
            };
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return new ProcessStartInfo
            {
                FileName = "osascript",
                Arguments = $"-e 'tell application \"Terminal\" to do script \"{filePath}\"'",
                UseShellExecute = false,
            };
        }

        string[] terminals =
        [
            "gnome-terminal", "konsole", "xfce4-terminal",
            "xterm", "lxterminal", "mate-terminal",
        ];

        foreach (string terminal in terminals)
        {
            if (!IsCommandAvailable(terminal))
            {
                continue;
            }

            string args = terminal switch
            {
                "gnome-terminal" => $"-- bash -c '{filePath}'; exec bash",
                "konsole" => $"-e bash -c '{filePath}'; exec bash",
                _ => $"-e bash -c '{filePath}'; exec bash",
            };

            return new ProcessStartInfo
            {
                FileName = terminal,
                Arguments = args,
                UseShellExecute = false,
            };
        }

        return new ProcessStartInfo
        {
            FileName = filePath,
            UseShellExecute = true,
        };
    }
    
    private static bool IsCommandAvailable(string command)
    {
        Process proc = new()
        {
            StartInfo = new()
            {
                FileName = "which",
                Arguments = command,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };

        proc.Start();
        proc.WaitForExit();
        
        return proc.ExitCode == 0;
    }
    
    public static bool IsExecutable(string path)
    {
        if (OperatingSystem.IsLinux())
        {
            return access(path, X_OK) == 0;
        }
        
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
    
    public static bool RequiresTerminal(string path)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return GetWindowsSubsystem(path) == 3;
}

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return !IsLinuxGuiBinary(path);
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return !IsMacOsGuiBinary(path);
        }

        return true;
    }

    private static int GetWindowsSubsystem(string path)
    {
        using FileStream fs = new(path, FileMode.Open, FileAccess.Read);
        using BinaryReader reader = new(fs);

        if (reader.ReadUInt16() != 0x5A4D)
        {
            return -1;
        }

        fs.Seek(0x3C, SeekOrigin.Begin);
        int peOffset = reader.ReadInt32();

        fs.Seek(peOffset, SeekOrigin.Begin);

        if (reader.ReadUInt32() != 0x00004550)
        {
            return -1;
        }

        fs.Seek(peOffset + 4 + 20 + 68, SeekOrigin.Begin);
        return reader.ReadUInt16();
    }

    private static bool IsLinuxGuiBinary(string path)
    {
        Process proc = new()
        {
            StartInfo = new()
            {
                FileName = "ldd",
                Arguments = path,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };

        proc.Start();
        string output = proc.StandardOutput.ReadToEnd();
        proc.WaitForExit();

        return output.Contains("libX11")
               || output.Contains("libwayland")
               || output.Contains("libQt")
               || output.Contains("libgtk");
    }

    private static bool IsMacOsGuiBinary(string path)
    {
        string? dir = Path.GetDirectoryName(path);
        return dir != null && dir.Contains(".app/");
    }
}