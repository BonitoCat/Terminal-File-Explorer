using System.Runtime.InteropServices;

namespace FileExplorer.Core.Utilities;

public static partial class FileUtils
{
    private const int XOk = 1;

    [LibraryImport("libc.so.6", EntryPoint = "access", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    private static partial int LinuxAccess(string pathname, int mode);

    [LibraryImport("libSystem.dylib", EntryPoint = "access", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    private static partial int MacAccess(string pathname, int mode);

    [LibraryImport("kernel32.dll", EntryPoint = "GetBinaryTypeW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetBinaryType(string applicationName, out uint binaryType);

    public static bool IsFileExecutable(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return false;
        }

        if (OperatingSystem.IsLinux())
        {
            return LinuxAccess(path, XOk) == 0;
        }

        if (OperatingSystem.IsMacOS())
        {
            return MacAccess(path, XOk) == 0;
        }

        if (OperatingSystem.IsWindows())
        {
            if (GetBinaryType(path, out _))
            {
                return true;
            }

            string extension = Path.GetExtension(path);
            return extension.Equals(".bat", StringComparison.OrdinalIgnoreCase)
                   || extension.Equals(".cmd", StringComparison.OrdinalIgnoreCase)
                   || extension.Equals(".ps1", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }
}