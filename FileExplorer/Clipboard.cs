using System.IO.MemoryMappedFiles;
using System.Text;
using FileLib;

namespace FileExplorer;

public static class Clipboard
{
    private const string Name = "fe.clipboard";
    private const int Size = 16 * 1024;

    public static void Write(ClipboardMode mode, IEnumerable<string> paths)
    {
        string filePath = Path.Combine(DirectoryHelper.GetCacheDirPath(), Name);
        
        using MemoryMappedFile memFile = MemoryMappedFile.CreateFromFile(filePath, FileMode.OpenOrCreate, null, Size);
        using MemoryMappedViewStream stream = memFile.CreateViewStream();
        using BinaryWriter writer = new(stream, Encoding.UTF8, leaveOpen: false);

        string[] array = paths.ToArray();

        writer.Write((int) mode);
        writer.Write(array.Length);
        
        foreach (string path in array)
        {
            writer.Write(path);
        }
    }

    public static void Read(out ClipboardMode mode, out string[] paths)
    {
        string filePath = Path.Combine(DirectoryHelper.GetCacheDirPath(), Name);
        
        using MemoryMappedFile memFile = MemoryMappedFile.CreateFromFile(filePath, FileMode.OpenOrCreate, null, Size);
        using MemoryMappedViewStream stream = memFile.CreateViewStream();
        using BinaryReader reader = new(stream, Encoding.UTF8, leaveOpen: false);

        mode = (ClipboardMode) reader.ReadInt32();
        int count = reader.ReadInt32();

        paths = new string[count];
        for (int i = 0; i < count; i++)
        {
            paths[i] = reader.ReadString();
        }
    }

    public static void Clear()
    {
        string filePath = Path.Combine(DirectoryHelper.GetCacheDirPath(), Name);
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }
}