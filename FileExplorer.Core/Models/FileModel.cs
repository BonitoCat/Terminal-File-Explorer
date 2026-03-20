namespace FileExplorer.Core.Models;

public class FileModel : IFileSystemEntry
{
    public required string Name { get; set; }
    public required string FullPath { get; set; }
    public EntryType Type => EntryType.File;
    public FileType FileType { get; set; } = FileType.Unknown;
    public Action<IFileSystemEntry> OnChanged { get; set; } = file => { };
    public Action<IFileSystemEntry> OnClicked { get; set; } = file => { };
}
