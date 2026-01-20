using System.Diagnostics;
using InputLib.EventArgs;

namespace FileExplorer.Keybinds;

public class CopyPathKeybind(MenuContext context) : Keybind(context)
{
    public override void OnKeyDown(KeyDownEventArgs e)
    {
        if (OperatingSystem.IsLinux())
        {
            Copy(Directory.GetCurrentDirectory());
        }
    }
    
    public static void Copy(string text)
    {
        if (IsWayland() && File.Exists("/usr/bin/wl-copy"))
        {
            Run("wl-copy", text);
            return;
        }

        if (File.Exists("/usr/bin/xclip"))
        {
            Run("xclip -selection clipboard", text);
            return;
        }

        throw new InvalidOperationException("No clipboard utility found (wl-copy or xclip).");
    }

    private static bool IsWayland()
    {
        string? wayland = Environment.GetEnvironmentVariable("WAYLAND_DISPLAY");
        return !string.IsNullOrEmpty(wayland);
    }

    private static void Run(string command, string input)
    {
        ProcessStartInfo psi = new()
        {
            FileName = "/bin/bash",
            Arguments = "-c \"" + command + "\"",
            RedirectStandardInput = true,
            UseShellExecute = false
        };

        using Process process = Process.Start(psi)!;
        process.StandardInput.Write(input);
        process.StandardInput.Close();
        process.WaitForExit();
    }
}