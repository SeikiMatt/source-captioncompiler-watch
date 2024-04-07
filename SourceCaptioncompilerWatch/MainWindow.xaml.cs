using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using System.Diagnostics;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Timers;
using System.Windows.Controls;

namespace SourceCaptioncompilerWatch;

public partial class MainWindow
{
    private string _lastFolderBrowsed = "C:\\";
    private string _resourcePath = "";
    private bool _watching;
    private readonly List<FileItem> _fileList = [];
    private readonly FileSystemWatcher _closecaptionWatcher = new();
    private readonly FileSystemWatcher _subtitlesWatcher = new();

    public MainWindow()
    {
        InitializeComponent();

        _closecaptionWatcher.Changed += OnFileChanged;
        _subtitlesWatcher.Changed += OnFileChanged;

        var aTimer = new System.Timers.Timer();
        aTimer.Elapsed += OnTimedEvent;
        aTimer.Interval = 1000;
        aTimer.Enabled = true;

        ButtonFolderPathLoad.IsEnabled = false;
    }

    private void OnTimedEvent(object? source, ElapsedEventArgs e)
    {
        foreach (var entry in _fileList)
        {
            var timeDifference = DateTime.Now.Subtract(entry.Changed);
            var hr = timeDifference.Hours > 0 ? $"{timeDifference.Hours}h " : "";
            var min = timeDifference.Minutes > 0 ? $"{timeDifference.Minutes}m " : "";
            var sec = $"{timeDifference.Seconds}s ago";

            if (entry.Status == "Unchanged") continue;

            var entryMatch = _fileList.First(itemToCompare => entry.Path == itemToCompare.Path);
            entryMatch.Status = $"Compiled {hr}{min}{sec}";
        }
    }

    private void OnFileChanged(object source, FileSystemEventArgs e)
    {
        var process = new Process();

        process.StartInfo.FileName = "cmd";
        process.StartInfo.Arguments =
            $"/K \"cd /d {_resourcePath}\\captioncompiler && captioncompiler.exe {Util.FilePathFinalEntry(e.FullPath)} -d 0 && exit";
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;

        process.Start();
        process.WaitForExit();

        var entryMatch =
            _fileList.First(itemToCompare => Util.FilePathFinalEntry(e.FullPath) == itemToCompare.Path);
        entryMatch.Changed = DateTime.Now;
        entryMatch.Status = $"Compiled";

        _fileList.Sort((a, b) => DateTime.Compare(b.Changed, a.Changed));
        
        Dispatcher.Invoke(() =>
        {
            ConsoleDisplay.Text = process.StandardOutput.ReadToEnd();
            FileListPanel.Items.Refresh();
            FileListPanel.ScrollIntoView(_fileList.First());
        });
    }

    private void TextBoxFolderPath_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        ButtonFolderPathLoad.IsEnabled = TextBoxFolderPath.Text.Length != 0;
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

    private void ButtonWatchAndCompile_OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        _watching = !_watching;

        if (_watching)
        {
            _closecaptionWatcher.Path = _resourcePath;
            _closecaptionWatcher.NotifyFilter = NotifyFilters.LastWrite;
            _closecaptionWatcher.Filter = "closecaption_*.txt";
            _closecaptionWatcher.EnableRaisingEvents = true;

            _subtitlesWatcher.Path = _resourcePath;
            _subtitlesWatcher.NotifyFilter = NotifyFilters.LastWrite;
            _subtitlesWatcher.Filter = "subtitles_*.txt";
            _subtitlesWatcher.EnableRaisingEvents = true;

            TextBoxFolderPath.IsEnabled = false;
            ButtonFolderPathLoad.IsEnabled = false;
            ButtonBrowse.IsEnabled = false;

            this.Title = "Source Captioncompiler Watcher - Watching";

            Dispatcher.Invoke(() => { ButtonWatchAndCompile.Content = "Stop Watching"; });
        }
        else
        {
            _closecaptionWatcher.EnableRaisingEvents = false;
            _subtitlesWatcher.EnableRaisingEvents = false;

            TextBoxFolderPath.IsEnabled = true;
            ButtonFolderPathLoad.IsEnabled = true;
            ButtonBrowse.IsEnabled = true;

            this.Title = "Source Captioncompiler Watcher - Idle";

            Dispatcher.Invoke(() => { ButtonWatchAndCompile.Content = "Watch"; });
        }
    }

    private void FolderPathLoad_OnMouseDown(object sender, MouseButtonEventArgs mouseButtonEventArgs)
    {
        HandleNewFolder(TextBoxFolderPath.Text);
    }

    private static void CheckInvalidCompilerDist(string distCompilerPath)
    {
        var isDistCompilerValid = Util.ValidateCompilerFiles(distCompilerPath);

        if (isDistCompilerValid) return;

        MessageBox.Show(
            "Included captioncompiler distribution is missing one or more files. Please replace the files or re-download this application.",
            "Missing files", MessageBoxButton.OK,
            MessageBoxImage.Error);

        Application.Current.Shutdown();
    }

    private static void CheckInvalidCompilerResource(string resourceCompilerPath, string distCompilerPath)
    {
        var isResourceCompilerValid = Util.ValidateCompilerFiles(resourceCompilerPath);

        if (isResourceCompilerValid) return;

        var result = MessageBox.Show(
            "The required captioncompiler files are fully or partially missing in resource folder, do you wish to copy the included distribution to the selected resource folder?",
            "Missing files", MessageBoxButton.YesNo,
            MessageBoxImage.Error);

        if (result == MessageBoxResult.Yes)
        {
            Directory.CreateDirectory(resourceCompilerPath);
            Util.CopyDirectory(distCompilerPath, resourceCompilerPath, true);
        }
        else
        {
            Application.Current.Shutdown();
        }
    }

    private void HandleNewFolder(string folderPath)
    {
        // store last resource folder path
        _lastFolderBrowsed = folderPath;

        // check if provided path ends in "resource"
        var resourceFolderEnd = Util.FilePathFinalEntry(folderPath);
        if (resourceFolderEnd != "resource" && resourceFolderEnd != "resource\\")
        {
            MessageBox.Show(
                "Expected selected folder to be named \"resource\".",
                "Bad folder name", MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }

        _resourcePath = folderPath;

        var resourceCompilerPath = folderPath + "\\captioncompiler";
        var distCompilerPath = AppDomain.CurrentDomain.BaseDirectory + "captioncompiler";

        CheckInvalidCompilerResource(resourceCompilerPath, distCompilerPath);
        CheckInvalidCompilerDist(distCompilerPath);

        // get path to all compilable text files in resource folder
        var resourceFilePaths =
            Directory.EnumerateFiles(folderPath, "*.txt")
                .Where(s =>
                {
                    var fileName = Util.FilePathFinalEntry(s);
                    return fileName.StartsWith("closecaption_") ||
                           fileName.StartsWith("subtitles_");
                })
                .ToArray();

        // check if any compilable files were found
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

        // update file count display
        ConsoleDisplay.Text = $"Found {resourceFilePaths.Length} files";

        // build game folder path from resource folder path
        var gameFolderPath = string.Join("\\",
            folderPath
                .Split("\\")
                .Where(v => v != "")
                .ToArray()[..^1]);

        // get game info file path from game folder path
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

        _fileList.Clear();

        foreach (var path in resourceFilePaths)
        {
            _fileList.Add(new FileItem(Util.FilePathFinalEntry(path), "Unchanged"));
        }

        FileListPanel.Items.Refresh();
        FileListPanel.ItemsSource = _fileList;

        ButtonWatchAndCompile.IsEnabled = true;
    }

    private class FileItem(string path, string status) : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private DateTime _changed;


        public string Path => path;

        public string Status
        {
            get => status;
            set
            {
                status = value;
                NotifyPropertyChanged();
            }
        }

        public DateTime Changed
        {
            get => _changed;
            set
            {
                _changed = value;
                NotifyPropertyChanged();
            }
        }
    }

    [GeneratedRegex("""
                    "GameInfo"[\n\r]+{[\n\r]+\s*game\s+"(.+?)"[\n\r]+\s*title\s+"(.+?)"
                    """,
        RegexOptions.Singleline)]
    private static partial Regex RegexExtractGameAndTitle();
}