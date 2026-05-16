using System.IO;
using MeuMarkdown.Models;

namespace MeuMarkdown.Services;

public class WorkspaceService : IDisposable
{
    private static readonly HashSet<string> _hiddenDirs = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", ".vs", ".vscode", ".idea", "node_modules", "bin", "obj"
    };

    private static readonly HashSet<string> _markdownExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".md", ".markdown"
    };

    private FileSystemWatcher? _watcher;
    private bool _showAllFiles;

    public FileNode? Root { get; private set; }
    public string? RootPath { get; private set; }

    public event EventHandler? TreeChanged;

    public void Open(string directoryPath, bool showAllFiles)
    {
        Close();

        if (!Directory.Exists(directoryPath))
            return;

        _showAllFiles = showAllFiles;
        RootPath = directoryPath;
        Root = BuildNode(directoryPath);
        Root.IsExpanded = true;

        try
        {
            _watcher = new FileSystemWatcher(directoryPath)
            {
                IncludeSubdirectories = true,
                EnableRaisingEvents = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName
            };
            _watcher.Created += OnFsEvent;
            _watcher.Deleted += OnFsEvent;
            _watcher.Renamed += OnFsEvent;
        }
        catch
        {
            _watcher = null;
        }
    }

    public void Close()
    {
        if (_watcher != null)
        {
            _watcher.Created -= OnFsEvent;
            _watcher.Deleted -= OnFsEvent;
            _watcher.Renamed -= OnFsEvent;
            _watcher.Dispose();
            _watcher = null;
        }
        Root = null;
        RootPath = null;
    }

    public void Refresh()
    {
        if (RootPath == null) return;
        var path = RootPath;
        var show = _showAllFiles;
        Open(path, show);
        TreeChanged?.Invoke(this, EventArgs.Empty);
    }

    public IEnumerable<string> EnumerateMarkdownFiles()
    {
        if (RootPath == null || !Directory.Exists(RootPath)) yield break;

        var stack = new Stack<string>();
        stack.Push(RootPath);
        while (stack.Count > 0)
        {
            var dir = stack.Pop();
            string[] subdirs;
            string[] files;
            try
            {
                subdirs = Directory.GetDirectories(dir);
                files = Directory.GetFiles(dir);
            }
            catch
            {
                continue;
            }

            foreach (var sub in subdirs)
            {
                var name = Path.GetFileName(sub);
                if (_hiddenDirs.Contains(name) || name.StartsWith('.')) continue;
                stack.Push(sub);
            }
            foreach (var f in files)
            {
                var ext = Path.GetExtension(f);
                if (_markdownExtensions.Contains(ext))
                    yield return f;
            }
        }
    }

    private FileNode BuildNode(string path)
    {
        var name = Path.GetFileName(path);
        if (string.IsNullOrEmpty(name)) name = path;
        var isDir = Directory.Exists(path);
        var node = new FileNode(name, path, isDir);

        if (isDir)
        {
            string[] dirs;
            string[] files;
            try
            {
                dirs = Directory.GetDirectories(path);
                files = Directory.GetFiles(path);
            }
            catch
            {
                return node;
            }

            Array.Sort(dirs, StringComparer.OrdinalIgnoreCase);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);

            foreach (var d in dirs)
            {
                var dirName = Path.GetFileName(d);
                if (_hiddenDirs.Contains(dirName) || dirName.StartsWith('.')) continue;
                var childNode = BuildNode(d);
                if (childNode.Children.Count > 0 || _showAllFiles)
                    node.Children.Add(childNode);
            }
            foreach (var f in files)
            {
                if (!_showAllFiles)
                {
                    var ext = Path.GetExtension(f);
                    if (!_markdownExtensions.Contains(ext)) continue;
                }
                var fileName = Path.GetFileName(f);
                node.Children.Add(new FileNode(fileName, f, isDirectory: false));
            }
        }

        return node;
    }

    private void OnFsEvent(object sender, FileSystemEventArgs e)
    {
        if (RootPath != null)
        {
            Root = BuildNode(RootPath);
            Root.IsExpanded = true;
            TreeChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Dispose() => Close();
}
