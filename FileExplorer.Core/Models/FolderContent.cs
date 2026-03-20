namespace FileExplorer.Core.Models;

public class FolderContent
{
    public required string Path { get; set; }
    public List<IFileSystemEntry> Entries { get; } = [];
}
