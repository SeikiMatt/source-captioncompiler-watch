using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;

namespace SourceCaptioncompilerWatch;

public partial class MainWindow
{
    private string _lastFolderBrowsed = "C:\\";
    private readonly List<FileItem> _fileList = [];

    public MainWindow()
    {
        InitializeComponent();
    }

    private static string FilePathFinalEntry(string path)
    {
        var pathSplit = path.Split("\\");
        return pathSplit.Last(v => v.Length > 0);
    }

    private static IEnumerable<string> FilePathFinalEntry(IEnumerable<string> paths)
    {
        string[] fileNames = [];
        fileNames = paths.Aggregate(fileNames,
            (current, path) => current.Append(path.Split("\\").Last(v => v.Length > 0)).ToArray());
        return fileNames;
    }

    private static void CreateTextFileWatcher(string path)
    {
        var watcher = new FileSystemWatcher();
        watcher.Path = path;

        watcher.NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.LastWrite;
        watcher.Filter = "*.txt";

        watcher.Changed += OnChanged;

        watcher.EnableRaisingEvents = true;
    }

    private static void OnChanged(object source, FileSystemEventArgs e)
    {
        Console.WriteLine("File: " + e.FullPath + " " + e.ChangeType);
    }

    private void ButtonBrowse_OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        var folderDialog = new OpenFolderDialog
        {
            Title = "Select Source Game Resource Folder",
            InitialDirectory = _lastFolderBrowsed,
        };

        if (folderDialog.ShowDialog() != true) return;

        TextBoxFolderPath.Text = folderDialog.FolderName;
        HandleNewFolder(folderDialog.FolderName);
    }

    private void FolderPathLoad_OnMouseDown(object sender, MouseButtonEventArgs mouseButtonEventArgs)
    {
        HandleNewFolder(TextBoxFolderPath.Text);
    }

    private class FileItem
    {
        public required string Path { get; set; }
        public required string Status { get; set; }
        public required string Changed { get; set; }
    }

    private void HandleNewFolder(string folderPath)
    {
        _lastFolderBrowsed = folderPath;

        var resourceFolderEnd = FilePathFinalEntry(folderPath);
        if (resourceFolderEnd != "resource" && resourceFolderEnd != "resource\\")
        {
            MessageBox.Show(
                "Expected selected folder to be named \"resource\".",
                "Bad folder name", MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }

        var resourceFilePaths =
            Directory.EnumerateFiles(folderPath, "*.txt")
                .Where(s =>
                {
                    var fileName = FilePathFinalEntry(s);
                    return fileName.StartsWith("closecaption_") ||
                           fileName.StartsWith("subtitles_");
                })
                .ToArray();

        if (resourceFilePaths.Length == 0)
        {
            MessageBox.Show(
                "Could not find any files that can be compiled in resource folder.\n" +
                "Expected .txt files starting with \"closecaption_\" or \"subtitles_\".\n" +
                "Example: closecaption_thai.txt",
                "File not found", MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }

        TextBlockStatus.Text = $"Found {resourceFilePaths.Length} files";
        var resourceFileNames = FilePathFinalEntry(resourceFilePaths);

        var gameFolderPath = string.Join("\\",
            folderPath
                .Split("\\")
                .Where(v => v != "")
                .ToArray()[..^1]);

        var gameinfoPath = $"{gameFolderPath}\\gameinfo.txt";

        var gameinfoGame = "";
        var gameinfoTitle = "";

        if (File.Exists(gameinfoPath))
        {
            var gameinfoText = File.OpenText(gameinfoPath).ReadToEnd();
            var match = RegexExtractGameAndTitle().Match(gameinfoText);
            if (match.Success)
            {
                gameinfoGame = match.Groups[1].Value;
                gameinfoTitle = match.Groups[2].Value;
            }

            TextBlockGameTitle.Text = gameinfoGame + "\n" + gameinfoTitle;
        }
        else
        {
            MessageBox.Show(
                "\"gameinfo.txt\" not found in resource folder's parent folder.\nAre you sure your resource folder is inside a Source game folder?",
                "File not found", MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }

        _fileList.RemoveAll(_ => true);
        _fileList.AddRange(resourceFileNames.Select(path => new FileItem()
            { Path = path, Status = "Unchanged", Changed = "Never" }));

        FileListPanel.Items.Refresh();
        FileListPanel.ItemsSource = _fileList;
    }

    [GeneratedRegex("""
                    "GameInfo"[\n\r]+{[\n\r]+\s*game\s+"(.+?)"[\n\r]+\s*title\s+"(.+?)"
                    """,
        RegexOptions.Singleline)]
    private static partial Regex RegexExtractGameAndTitle();
}