using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using FileExplorer.Tui.Context;
using FileExplorer.Tui.FileTypes;
using InputLib;
using InputLib.EventArgs;
using LoggerLib;
using TuiLib;
using TuiLib.Controls;

namespace FileExplorer.Tui.Keybinds;

public class MetadataKeybind(IEntryContext context, List<IEntryContext> contexts) : Keybind(context)
{
    [DllImport("libc", SetLastError = true)]
    private static extern int stat(string path, out StatBuffer statbuf);

    [StructLayout(LayoutKind.Sequential)]
    private struct StatBuffer
    {
        public ulong st_dev;
        public ulong st_ino;
        public ulong st_nlink;
        public uint  st_mode;
        public uint  st_uid;
        public uint  st_gid;
        public uint  _pad0;
        public ulong st_rdev;
        public long  st_size;
        public long  st_blksize;
        public long  st_blocks;
        public long  st_atime;
        public long  st_atime_nsec;
        public long  st_mtime;
        public long  st_mtime_nsec;
        public long  st_ctime;
        public long  st_ctime_nsec;
        public long  _unused0;
        public long  _unused1;
        public long  _unused2;
    }

    private readonly TuiRenderer _renderer = new();
    private readonly ManualResetEventSlim _closeEvent = new(false);

    private TuiListBox<TuiLabel> _list = new();
    private TuiBorder _border = new() { Title = "Properties" };

    private string _filePath = "";
    private readonly object _renderLock = new();

    public override void OnKeyDown(KeyDownEventArgs e)
    {
        Logger.LogI("Item metadata requested");
        TuiListBoxItem<TuiLabel>? item = _context.SelectedItems.Count == 1
            ? _context.SelectedItems[0]
            : _context.Menu.SelectedItem;

        if (item == null || item.Item?.Text == "..")
        {
            Logger.LogI("Metadata request canceled");
            return;
        }
        
        _filePath = Path.GetFullPath(item.Item.Text);
        _closeEvent.Reset();

        bool[] isDrawingEnabled = contexts.Select(context => context.CanDraw).ToArray();
        
        contexts.ForEach(context => context.DisableDrawing());
        _context.Listener.PauseListening = true;
        
        ShowPopup();

        for (int i = 0; i < contexts.Count; i++)
        {
            if (isDrawingEnabled[i])
            {
                contexts[i].EnableDrawing();
            }
        }
        
        _context.RedrawMenu();
        
        _context.Listener.ConsumeNextKeyDown(Key.Escape);
        _context.Listener.PauseListening = false;
        
        Logger.LogI("Closing metadata window");
    }

    private void ShowPopup()
    {
        Logger.LogI("Opening metadata window");
        
        InputListener? keyListener = InputListener.New();
        if (keyListener == null)
        {
            Logger.LogW("Could not load listener... returning");
            return;
        }
        
        void OnItemAdded(TuiListBoxItem<TuiLabel> _)
        {
            OnWindowResize();
        }
        
        lock (_context.OutLock)
        {
            _list = new();
            
            int popupWidth  = Math.Min(Console.WindowWidth  - 4, 52);
            int popupHeight = Math.Min(Console.WindowHeight - 4, _list.GetItemCount());

            _list = new TuiListBox<TuiLabel>
            {
                MaxWidth = popupWidth,
                MaxHeight = popupHeight,
                X = Console.WindowWidth  / 2,
                Y = Console.WindowHeight / 2,
                AnchorPoint = AnchorPoint.Center,
                ScrollBarColor = Color.Gray,
            };

            _list.OnItemAdded += OnItemAdded;
            
            _border = new TuiBorder
            {
                Title = new TuiLabel(Path.GetFileName(_filePath)),
                Child = _list,
            };

            AddSection("General");
            AddRow("Loading...", "");
            RenderPopup();

            Task.Run(LoadGeneralSection);
            Task.Run(LoadPermissionsSection);
            Task.Run(LoadTypeSpecificSection);
            
            Task.Run(() =>
            {
                while (Console.KeyAvailable)
                {
                    Console.ReadKey(true);
                }
                
                while (!_closeEvent.IsSet)
                {
                    if (!Console.KeyAvailable)
                    {
                        Thread.Sleep(16);
                        continue;
                    }
                    
                    ConsoleKeyInfo info = Console.ReadKey(true);
                    switch (info.Key)
                    {
                        case ConsoleKey.Escape:
                            _closeEvent.Set();
                        break;
                            
                        case ConsoleKey.DownArrow:
                            if (_list.Scroll(1))
                            {
                                RenderPopup();
                            }
                        break;
                            
                        case ConsoleKey.UpArrow:
                            if (_list.Scroll(-1))
                            {
                                RenderPopup();
                            }
                        break;
                    }
                }
            });
        }
        
        WindowManager.Instance.MainWindow.OnWindowResize += OnWindowResize;
        _closeEvent.Wait();
        
        _list.OnItemAdded -= OnItemAdded;
        WindowManager.Instance.MainWindow.OnWindowResize -= OnWindowResize;

        lock (_context.OutLock)
        {
            Console.Clear();
        }
        
        contexts.ForEach(context => context.RedrawMenu());
    }

    private void LoadGeneralSection()
    {
        List<(string, string)> rows = [];

        try
        {
            FileInfo info = new(_filePath);
            rows.Add(("Name", info.Name));
            rows.Add(("Type", GetFileTypeLabel()));
            rows.Add(("Size", FormatSize(info.Length)));
            rows.Add(("Modified", info.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss")));
            rows.Add(("Created", info.CreationTime.ToString("yyyy-MM-dd HH:mm:ss")));
            rows.Add(("Accessed", info.LastAccessTime.ToString("yyyy-MM-dd HH:mm:ss")));
        }
        catch (Exception ex)
        {
            rows.Add(("Error", ex.Message));
        }

        lock (_renderLock)
        {
            ReplaceSection("General", rows);
        }
    }

    private void LoadPermissionsSection()
    {
        List<(string, string)> rows = [];

        try
        {
            if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            {
                if (stat(_filePath, out StatBuffer sb) == 0)
                {
                    uint mode = sb.st_mode;
                    rows.Add(("Permissions", FormatUnixPermissions(mode)));
                    rows.Add(("Octal",       $"{mode & 0xFFF:000}"));
                    rows.Add(("Owner UID",   sb.st_uid.ToString()));
                    rows.Add(("Group GID",   sb.st_gid.ToString()));

                    bool isExec = (mode & 0b001001001) != 0;
                    rows.Add(("Executable", isExec ? "Yes" : "No"));
                }
            }
            
            FileInfo info = new(_filePath);
            FileAttributes attrs = info.Attributes;
            rows.Add(("Read-only", attrs.HasFlag(FileAttributes.ReadOnly) ? "Yes" : "No"));
            rows.Add(("Hidden", attrs.HasFlag(FileAttributes.Hidden) ? "Yes" : "No"));
            rows.Add(("System", attrs.HasFlag(FileAttributes.System) ? "Yes" : "No"));
        }
        catch (Exception ex)
        {
            rows.Add(("Error", ex.Message));
        }

        lock (_renderLock)
        {
            ReplaceSection("Permissions", rows);
        }
    }

    private void LoadTypeSpecificSection()
    {
        string ext = Path.GetExtension(_filePath).ToLowerInvariant();
        List<(string, string)> rows = [];

        try
        {
            if (ExecutableFile.IsExecutable(_filePath))
            {
                rows.Add(("Needs terminal", ExecutableFile.RequiresTerminal(_filePath) ? "Yes" : "No"));

                if (OperatingSystem.IsLinux())
                {
                    string lddOutput = RunProcess("ldd", _filePath);
                    int libCount = lddOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;
                    rows.Add(("Linked libs", libCount.ToString()));

                    bool isGui = lddOutput.Contains("libX11") ||
                                 lddOutput.Contains("libwayland") ||
                                 lddOutput.Contains("libQt") ||
                                 lddOutput.Contains("libgtk");
                    
                    rows.Add(("GUI binary", isGui ? "Yes" : "No"));
                }
            }
            else if (IsImageExtension(ext))
            {
                LoadImageMetadata(rows);
            }
            else if (IsMediaExtension(ext))
            {
                LoadMediaMetadata(rows);
            }
            else if (IsTextExtension(ext))
            {
                LoadTextMetadata(rows);
            }
            else if (IsArchiveExtension(ext))
            {
                LoadArchiveMetadata(rows);
            }
        }
        catch (Exception ex)
        {
            rows.Add(("Error", ex.Message));
        }

        if (rows.Count == 0)
        {
            return;
        }

        lock (_renderLock)
        {
            ReplaceSection("Details", rows);
        }
    }

    private void LoadImageMetadata(List<(string, string)> rows)
    {
        string ext = Path.GetExtension(_filePath).ToLowerInvariant();
        (int width, int height) = ReadImageDimensions(ext);
        
        if (width > 0)
        {
            rows.Add(("Resolution", $"{width} x {height}"));
        }

        string exif = RunProcess("exiftool", $"-Make -Model -DateTimeOriginal -GPSLatitude -GPSLongitude -s3 \"{_filePath}\"");
        if (!string.IsNullOrWhiteSpace(exif))
        {
            string[] lines = exif.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            string[] labels = ["Camera make", "Camera model", "Taken", "GPS lat", "GPS lon"];
            
            for (int i = 0; i < Math.Min(lines.Length, labels.Length); i++)
            {
                rows.Add((labels[i], lines[i].Trim()));
            }
        }
    }

    private void LoadMediaMetadata(List<(string, string)> rows)
    {
        string duration = RunProcess("ffprobe",
            $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{_filePath}\"");
        
        if (!string.IsNullOrWhiteSpace(duration) && double.TryParse(duration.Trim(), out double secs))
        {
            rows.Add(("Duration", TimeSpan.FromSeconds(secs).ToString(@"hh\:mm\:ss")));
        }

        string info = RunProcess("ffprobe",
            $"-v error -select_streams v:0 -show_entries stream=codec_name,width,height,bit_rate -of default=noprint_wrappers=1 \"{_filePath}\"");
        
        foreach (string line in info.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] parts = line.Split('=');
            if (parts.Length != 2)
            {
                continue;
            }
            
            rows.Add((parts[0].Trim() switch
            {
                "codec_name" => "Codec",
                "width" => "Width",
                "height" => "Height",
                "bit_rate" => "Bitrate",
                _ => parts[0].Trim(),
            }, parts[1].Trim()));
        }
    }

    private void LoadTextMetadata(List<(string, string)> rows)
    {
        string[] lines = File.ReadAllLines(_filePath);
        int wordCount = lines.Sum(l => l.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length);
        
        rows.Add(("Lines", lines.Length.ToString()));
        rows.Add(("Words", wordCount.ToString()));
        rows.Add(("Encoding", DetectEncoding(_filePath)));
    }

    private void LoadArchiveMetadata(List<(string, string)> rows)
    {
        string ext = Path.GetExtension(_filePath).ToLowerInvariant();
        string output = ext switch
        {
            ".zip" => RunProcess("unzip", $"-l \"{_filePath}\""),
            ".tar" => RunProcess("tar", $"-tf \"{_filePath}\""),
            ".gz" => RunProcess("tar", $"-tzf \"{_filePath}\""),
            ".bz2" => RunProcess("tar", $"-tjf \"{_filePath}\""),
            ".xz" => RunProcess("tar", $"-tJf \"{_filePath}\""),
            ".7z" => RunProcess("7z", $"l \"{_filePath}\""),
            _ => "",
        };

        if (!string.IsNullOrWhiteSpace(output))
        {
            int fileCount = output.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;
            rows.Add(("Files inside", fileCount.ToString()));
        }
    }

    private void AddSection(string title)
    {
        _list.AddItem(new TuiListBoxItem<TuiLabel>(new TuiLabel($" ── {title}")));
    }

    private void AddRow(string key, string value)
    {
        string padded = $"  {key,-16} {TruncateValue(value)}";
        _list.AddItem(new TuiListBoxItem<TuiLabel>(new TuiLabel(padded)));
    }

    private void ReplaceSection(string sectionTitle, List<(string Key, string Value)> rows)
    {
        List<TuiListBoxItem<TuiLabel>> toRemove = new();
        bool inSection = false;

        foreach (TuiListBoxItem<TuiLabel> item in _list.Items)
        {
            string? text = item.Item?.Text;
            if (text == null)
            {
                continue;
            }

            if (text.Contains($"── {sectionTitle}"))
            {
                inSection = true;
                toRemove.Add(item);
                continue;
            }

            if (inSection)
            {
                if (text.Contains("──"))
                {
                    break;
                }

                toRemove.Add(item);
            }
        }

        int insertAt = toRemove.Count > 0 ? _list.IndexOf(toRemove[0]) : _list.GetItemCount();
        
        _list.RemoveItemRange(toRemove);
        _list.InsertItem(insertAt, new TuiListBoxItem<TuiLabel>(new TuiLabel($" ── {sectionTitle} ")));
        
        insertAt++;
        foreach ((string key, string value) in rows)
        {
            string padded = $"  {key,-16} {TruncateValue(value)}";
            _list.InsertItem(insertAt++, new TuiListBoxItem<TuiLabel>(new TuiLabel(padded)));
        }

        RenderPopup();
    }

    private void RenderPopup()
    {
        _renderer.Render(_border.Render());
    }

    private void OnWindowResize()
    {
        _list.MaxWidth  = Math.Min(Console.WindowWidth - 4, 52);
        _list.MaxHeight = Math.Min(Console.WindowHeight - 4, _list.GetItemCount());
        _list.X = Console.WindowWidth / 2;
        _list.Y = Console.WindowHeight / 2;

        lock (_context.OutLock)
        {
            Console.Clear();
            
            _context.EnableDrawing();
            contexts.ForEach(context => context.RedrawMenuSync());
            
            _context.DisableDrawing();
            RenderPopup();
        }
    }

    private string GetFileTypeLabel()
    {
        if (Directory.Exists(_filePath))
        {
            return "Folder";
        }

        if (ExecutableFile.IsExecutable(_filePath))
        {
            return "Executable";
        }
        
        string ext = Path.GetExtension(_filePath).ToLowerInvariant();
        return string.IsNullOrEmpty(ext) ? "File" : ext.TrimStart('.').ToUpperInvariant();
    }

    private static string FormatSize(long bytes)
    {
        string[] units = ["B", "KiB", "MiB", "GiB", "TiB"];
        double size = bytes;
        int unit = 0;

        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024; unit++;
        }
        
        return $"{size:0.##} {units[unit]} ({bytes:N0} bytes)";
    }

    private static string FormatUnixPermissions(uint mode)
    {
        StringBuilder sb = new(9);
        sb.Append((mode & 0x100) != 0 ? 'r' : '-');
        sb.Append((mode & 0x080) != 0 ? 'w' : '-');
        sb.Append((mode & 0x040) != 0 ? "x " : "- ");
        sb.Append((mode & 0x020) != 0 ? 'r' : '-');
        sb.Append((mode & 0x010) != 0 ? 'w' : '-');
        sb.Append((mode & 0x008) != 0 ? "x " : "- ");
        sb.Append((mode & 0x004) != 0 ? 'r' : '-');
        sb.Append((mode & 0x002) != 0 ? 'w' : '-');
        sb.Append((mode & 0x001) != 0 ? 'x' : '-');
        
        return sb.ToString();
    }

    private string TruncateValue(string value)
    {
        int maxVal = _list.MaxWidth - 20;
        if (maxVal < 4)
        {
            return value;
        }
        
        return value.Length > maxVal ? value[..(maxVal - 3)] + "..." : value;
    }

    private static string RunProcess(string fileName, string args)
    {
        try
        {
            Process proc = new()
            {
                StartInfo = new()
                {
                    FileName = fileName,
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
            };

            proc.Start();
            string output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit();
            
            return output;
        }
        catch
        {
            return "";
        }
    }

    private static string DetectEncoding(string path)
    {
        byte[] bom = new byte[4];
        using FileStream fs = new(path, FileMode.Open, FileAccess.Read);
        int read = fs.Read(bom, 0, 4);

        if (read >= 3 && bom[0] == 0xEF && bom[1] == 0xBB && bom[2] == 0xBF) return "UTF-8 BOM";
        if (read >= 2 && bom[0] == 0xFF && bom[1] == 0xFE) return "UTF-16 LE";
        if (read >= 2 && bom[0] == 0xFE && bom[1] == 0xFF) return "UTF-16 BE";
        
        return "UTF-8";
    }

    private (int width, int height) ReadImageDimensions(string ext)
    {
        try
        {
            using FileStream fs = new(_filePath, FileMode.Open, FileAccess.Read);
            using BinaryReader reader = new(fs);

            if (ext == ".png")
            {
                fs.Seek(16, SeekOrigin.Begin);
                byte[] width = reader.ReadBytes(4);
                byte[] height = reader.ReadBytes(4);

                if (BitConverter.IsLittleEndian)
                {
                    Array.Reverse(width); Array.Reverse(height);
                }
                
                return (BitConverter.ToInt32(width), BitConverter.ToInt32(height));
            }

            if (ext is ".jpg" or ".jpeg")
            {
                while (fs.Position < fs.Length - 9)
                {
                    if (reader.ReadByte() != 0xFF)
                    {
                        continue;
                    }
                    
                    byte marker = reader.ReadByte();
                    if (marker is >= 0xC0 and <= 0xC3)
                    {
                        fs.Seek(3, SeekOrigin.Current);
                        byte[] height = reader.ReadBytes(2);
                        byte[] width = reader.ReadBytes(2);
                        
                        if (BitConverter.IsLittleEndian)
                        {
                            Array.Reverse(width); Array.Reverse(height);
                        }
                        
                        return (BitConverter.ToInt16(width), BitConverter.ToInt16(height));
                    }
                }
            }
        }
        catch { }

        return (0, 0);
    }

    private static bool IsImageExtension(string ext) => ext is ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".webp" or ".tiff";
    private static bool IsMediaExtension(string ext) => ext is ".mp4" or ".mkv" or ".avi" or ".mov" or ".mp3" or ".flac" or ".wav" or ".ogg";
    private static bool IsTextExtension(string ext) => ext is ".txt" or ".md" or ".cs" or ".cpp" or ".h" or ".py" or ".js" or ".ts" or ".json" or ".xml" or ".yaml" or ".toml" or ".sh";
    private static bool IsArchiveExtension(string ext) => ext is ".zip" or ".tar" or ".gz" or ".bz2" or ".xz" or ".7z" or ".rar";
}