namespace FileExplorer.Core.Models;

public interface IFileSystemEntry
{
    public string Name { get; set; }
    public string FullPath { get; set; }
    public EntryType Type { get; }
    public Action<IFileSystemEntry> OnChanged { get; set; }
    public Action<IFileSystemEntry> OnClicked { get; set; }
}
