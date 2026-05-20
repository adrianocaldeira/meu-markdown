using System.IO;
using System.Runtime.InteropServices;
using MeuMarkdown.Extensions.WikiLinks;
using MeuMarkdown.Models;

namespace MeuMarkdown.Services;

public enum FileTreeSortMode
{
    /// <summary>Natural sort por nome (entende números — 31 antes de 292). Como o Windows Explorer.</summary>
    NameNatural,
    /// <summary>Data de modificação descendente (mais recente primeiro).</summary>
    DateModifiedDesc,
}

public class WorkspaceService : IDisposable
{
    [DllImport("Shlwapi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int StrCmpLogicalW(string a, string b);

    public FileTreeSortMode SortMode { get; set; } = FileTreeSortMode.NameNatural;

    private static readonly HashSet<string> _hiddenDirs = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", ".vs", ".vscode", ".idea", "node_modules", "bin", "obj"
    };

    private static readonly HashSet<string> _markdownExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".md", ".markdown"
    };

    private FileSystemWatcher? _watcher;
    private System.Timers.Timer? _debounceTimer;
    private bool _showAllFiles;

    private Dictionary<string, List<string>>? _wikiLinkIndex;
    private readonly object _wikiLinkLock = new();

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

        TreeChanged?.Invoke(this, EventArgs.Empty);
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
        if (_debounceTimer != null)
        {
            _debounceTimer.Stop();
            _debounceTimer.Dispose();
            _debounceTimer = null;
        }
        Root = null;
        RootPath = null;
        lock (_wikiLinkLock) { _wikiLinkIndex = null; }
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

    private void Sort(string[] paths)
    {
        switch (SortMode)
        {
            case FileTreeSortMode.DateModifiedDesc:
                Array.Sort(paths, (a, b) =>
                {
                    try
                    {
                        return File.GetLastWriteTime(b).CompareTo(File.GetLastWriteTime(a));
                    }
                    catch
                    {
                        return StrCmpLogicalW(a, b);
                    }
                });
                break;
            case FileTreeSortMode.NameNatural:
            default:
                // StrCmpLogicalW é o mesmo comparador que o Windows Explorer usa:
                // "pbi-31" vem antes de "pbi-292" (entende números embutidos).
                Array.Sort(paths, (a, b) =>
                    StrCmpLogicalW(Path.GetFileName(a), Path.GetFileName(b)));
                break;
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

            Sort(dirs);
            Sort(files);

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
        if (RootPath == null) return;

        // Debounce: reset timer; only rebuild when no events arrive for 800ms.
        // Janela maior agrupa rajadas (build/test, save em massa, git operations).
        _debounceTimer?.Stop();
        _debounceTimer ??= new System.Timers.Timer(800) { AutoReset = false };
        _debounceTimer.Elapsed -= OnDebounceTimerElapsed;
        _debounceTimer.Elapsed += OnDebounceTimerElapsed;
        _debounceTimer.Start();
    }

    private void OnDebounceTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        if (RootPath == null) return;
        try
        {
            // Snapshot do estado atual antes do rebuild — sem isso, todas as pastas
            // abertas colapsariam a cada save de arquivo (FsWatcher dispara muito).
            var expandedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string? selectedPath = null;
            if (Root != null) CollectState(Root, expandedPaths, ref selectedPath);

            var newRoot = BuildNode(RootPath);
            newRoot.IsExpanded = true;
            RestoreState(newRoot, expandedPaths, selectedPath);
            Root = newRoot;
            lock (_wikiLinkLock) { _wikiLinkIndex = null; }

            TreeChanged?.Invoke(this, EventArgs.Empty);
        }
        catch
        {
            // Silently ignore — directory may have been deleted mid-rebuild
        }
    }

    private static void CollectState(FileNode node, HashSet<string> expanded, ref string? selected)
    {
        if (node.IsExpanded) expanded.Add(node.FullPath);
        if (node.IsSelected) selected = node.FullPath;
        foreach (var c in node.Children) CollectState(c, expanded, ref selected);
    }

    private static void RestoreState(FileNode node, HashSet<string> expanded, string? selected)
    {
        if (expanded.Contains(node.FullPath)) node.IsExpanded = true;
        if (selected != null && string.Equals(node.FullPath, selected, StringComparison.OrdinalIgnoreCase))
            node.IsSelected = true;
        foreach (var c in node.Children) RestoreState(c, expanded, selected);
    }

    /// <summary>
    /// Resolve um wiki-link target → path absoluto. Sintaxe:
    /// - Nome simples ("Foo"): lookup case-insensitive no índice do workspace.
    /// - Path relativo ("Sub/Foo" ou "Sub/Foo.md"): resolve relativo ao currentFileDir.
    /// Para múltiplos candidatos com mesmo nome, escolhe o de menor distância LCA
    /// ao currentFileDir; empate é resolvido alfabeticamente.
    /// </summary>
    public WikiLinkResolution? ResolveWikiLink(string target, string? currentFileDir)
    {
        if (string.IsNullOrWhiteSpace(target)) return null;

        if (target.Contains('/') || target.Contains('\\'))
        {
            if (string.IsNullOrEmpty(currentFileDir)) return null;
            var relPath = target;
            if (!relPath.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                relPath += ".md";
            var fullPath = Path.GetFullPath(Path.Combine(currentFileDir, relPath));
            return File.Exists(fullPath)
                ? new WikiLinkResolution(fullPath, Path.GetFileNameWithoutExtension(fullPath))
                : null;
        }

        if (RootPath == null) return null;

        var index = GetOrBuildWikiLinkIndex();
        var key = target.ToLowerInvariant();
        if (!index.TryGetValue(key, out var candidates) || candidates.Count == 0)
            return null;

        if (candidates.Count == 1)
            return new WikiLinkResolution(candidates[0], target);

        var ranked = candidates
            .Select(p => new { Path = p, Dist = LcaDistance(p, currentFileDir) })
            .OrderBy(x => x.Dist)
            .ThenBy(x => x.Path, StringComparer.OrdinalIgnoreCase)
            .First();
        return new WikiLinkResolution(ranked.Path, target);
    }

    private Dictionary<string, List<string>> GetOrBuildWikiLinkIndex()
    {
        lock (_wikiLinkLock)
        {
            if (_wikiLinkIndex != null) return _wikiLinkIndex;
            var idx = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var path in EnumerateMarkdownFiles())
            {
                var name = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                if (!idx.TryGetValue(name, out var list))
                {
                    list = new List<string>();
                    idx[name] = list;
                }
                list.Add(path);
            }
            _wikiLinkIndex = idx;
            return idx;
        }
    }

    private static int LcaDistance(string candidatePath, string? currentDir)
    {
        if (string.IsNullOrEmpty(currentDir)) return int.MaxValue / 2;
        var candidateDir = Path.GetDirectoryName(candidatePath) ?? "";
        if (string.Equals(candidateDir, currentDir, StringComparison.OrdinalIgnoreCase)) return 0;

        var a = candidateDir.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        var b = currentDir.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);

        int common = 0;
        var minLen = Math.Min(a.Length, b.Length);
        while (common < minLen && string.Equals(a[common], b[common], StringComparison.OrdinalIgnoreCase))
            common++;

        return (a.Length - common) + (b.Length - common);
    }

    public void Dispose() => Close();
}
