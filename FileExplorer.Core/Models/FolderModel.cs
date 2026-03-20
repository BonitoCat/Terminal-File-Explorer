namespace FileExplorer.Core.Models;

public class FolderModel : IFileSystemEntry
{
    public required string Name { get; set; }
    public required string FullPath { get; set; }
    public EntryType Type => EntryType.Folder;
    public Action<IFileSystemEntry> OnChanged { get; set; } = folder => { };
    public Action<IFileSystemEntry> OnClicked { get; set; } = folder => { };
}
