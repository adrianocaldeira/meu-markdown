using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MeuMarkdown.Controls;
using MeuMarkdown.Models;
using MeuMarkdown.Services;

namespace MeuMarkdown.Controls.Panels;

public partial class ExplorerPanel : UserControl
{
    private WorkspaceService? _workspace;
    private RecentFilesService? _recent;
    private readonly FileOperationsService _fileOps = new();

    private System.Windows.Point _dragStartPoint;
    private FileNode? _dragSourceNode;

    public static readonly RoutedUICommand NewFileCommand = new("Novo arquivo", "NewFile", typeof(ExplorerPanel));
    public static readonly RoutedUICommand NewFolderCommand = new("Nova pasta", "NewFolder", typeof(ExplorerPanel));
    public static readonly RoutedUICommand CopyCommand = new("Copiar", "CopyEx", typeof(ExplorerPanel));
    public static readonly RoutedUICommand CutCommand = new("Recortar", "CutEx", typeof(ExplorerPanel));
    public static readonly RoutedUICommand PasteCommand = new("Colar", "PasteEx", typeof(ExplorerPanel));
    public static readonly RoutedUICommand DeleteCommand = new("Excluir", "DeleteEx", typeof(ExplorerPanel));

    public event EventHandler<string>? FileActivated;
    /// <summary>Disparado em single-click sobre arquivo — pra abrir em modo visualização apenas.</summary>
    public event EventHandler<string>? FilePreview;
    public event EventHandler? OpenFolderRequested;
    public event EventHandler? CloseWorkspaceRequested;
    public event EventHandler<bool>? ShowAllFilesChanged;
    public event EventHandler<FileTreeSortMode>? SortModeChanged;

    public ExplorerPanel()
    {
        InitializeComponent();
        CommandBindings.Add(new CommandBinding(NewFileCommand, OnNewFile));
        CommandBindings.Add(new CommandBinding(NewFolderCommand, OnNewFolder));
        CommandBindings.Add(new CommandBinding(CopyCommand, OnCopy));
        CommandBindings.Add(new CommandBinding(CutCommand, OnCut));
        CommandBindings.Add(new CommandBinding(PasteCommand, OnPaste));
        CommandBindings.Add(new CommandBinding(DeleteCommand, OnDelete));
    }

    public void Bind(WorkspaceService workspace, RecentFilesService recent, bool showAllFiles)
    {
        if (_workspace != null)
            _workspace.TreeChanged -= OnTreeChanged;

        _workspace = workspace;
        _recent = recent;
        _workspace.TreeChanged += OnTreeChanged;
        RecentList.ItemsSource = _recent.Items;
        ShowAllFilesItem.IsChecked = showAllFiles;
        ApplySortModeCheckmarks(workspace.SortMode);
        RefreshTree();
    }

    private void ApplySortModeCheckmarks(FileTreeSortMode mode)
    {
        SortByNameItem.IsChecked = mode == FileTreeSortMode.NameNatural;
        SortByDateItem.IsChecked = mode == FileTreeSortMode.DateModifiedDesc;
    }

    private void OnSortByName(object sender, RoutedEventArgs e)
    {
        ApplySortModeCheckmarks(FileTreeSortMode.NameNatural);
        SortModeChanged?.Invoke(this, FileTreeSortMode.NameNatural);
    }

    private void OnSortByDate(object sender, RoutedEventArgs e)
    {
        ApplySortModeCheckmarks(FileTreeSortMode.DateModifiedDesc);
        SortModeChanged?.Invoke(this, FileTreeSortMode.DateModifiedDesc);
    }

    private void OnTreeChanged(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(RefreshTree);
    }

    private void RefreshTree()
    {
        if (_workspace?.Root == null)
        {
            FileTree.ItemsSource = null;
            FileTree.Visibility = Visibility.Collapsed;
            EmptyState.Visibility = Visibility.Visible;
            WorkspaceNameText.Text = "EXPLORER";
        }
        else
        {
            FileTree.ItemsSource = _workspace.Root.Children;
            FileTree.Visibility = Visibility.Visible;
            EmptyState.Visibility = Visibility.Collapsed;
            WorkspaceNameText.Text = _workspace.Root.Name.ToUpperInvariant();
        }
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var filter = FilterBox.Text?.Trim() ?? "";
        if (_workspace?.Root == null) return;
        ApplyFilterRec(_workspace.Root, filter);
    }

    /// <summary>
    /// Expande os ancestrais do arquivo, marca-o como selecionado e rola o TreeView
    /// até ele ficar visível. No-op se o arquivo não está dentro do workspace ativo.
    /// </summary>
    public void RevealFile(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath) || _workspace?.Root == null) return;

        ClearSelection(_workspace.Root);
        var target = RevealRec(_workspace.Root, filePath);
        if (target == null) return;

        target.IsSelected = true;

        // Containers do TreeView só são gerados depois que IsExpanded propaga e o layout
        // atualiza. Agendar BringIntoView pra rodar depois do próximo render pass.
        Dispatcher.BeginInvoke(new Action(() =>
        {
            var container = FindContainer(FileTree, target);
            container?.BringIntoView();
        }), System.Windows.Threading.DispatcherPriority.Background);
    }

    private static FileNode? RevealRec(FileNode node, string targetPath)
    {
        if (string.Equals(node.FullPath, targetPath, StringComparison.OrdinalIgnoreCase))
            return node;
        if (!node.IsDirectory) return null;
        foreach (var c in node.Children)
        {
            var found = RevealRec(c, targetPath);
            if (found != null)
            {
                node.IsExpanded = true;
                return found;
            }
        }
        return null;
    }

    private static void ClearSelection(FileNode node)
    {
        if (node.IsSelected) node.IsSelected = false;
        foreach (var c in node.Children) ClearSelection(c);
    }

    private static TreeViewItem? FindContainer(ItemsControl parent, FileNode target)
    {
        if (parent == null) return null;
        parent.UpdateLayout();
        if (parent.ItemContainerGenerator.ContainerFromItem(target) is TreeViewItem direct)
            return direct;
        foreach (var item in parent.Items)
        {
            if (parent.ItemContainerGenerator.ContainerFromItem(item) is not TreeViewItem child)
                continue;
            var found = FindContainer(child, target);
            if (found != null) return found;
        }
        return null;
    }

    private bool ApplyFilterRec(FileNode node, string filter)
    {
        if (string.IsNullOrEmpty(filter))
        {
            node.IsVisible = true;
            foreach (var c in node.Children) ApplyFilterRec(c, filter);
            return true;
        }

        var selfMatches = node.Name.Contains(filter, StringComparison.OrdinalIgnoreCase);
        var childMatch = false;
        foreach (var c in node.Children)
            childMatch |= ApplyFilterRec(c, filter);

        node.IsVisible = selfMatches || childMatch;
        if (node.IsDirectory && childMatch && !string.IsNullOrEmpty(filter))
            node.IsExpanded = true;
        return node.IsVisible;
    }

    private void OnFilterChanged(object sender, TextChangedEventArgs e) => ApplyFilter();

    private void OnMenuClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.ContextMenu != null)
        {
            btn.ContextMenu.PlacementTarget = btn;
            btn.ContextMenu.IsOpen = true;
        }
    }

    private void OnOpenFolder(object sender, RoutedEventArgs e) => OpenFolderRequested?.Invoke(this, EventArgs.Empty);
    private void OnRefresh(object sender, RoutedEventArgs e) => _workspace?.Refresh();
    private void OnCloseWorkspace(object sender, RoutedEventArgs e) => CloseWorkspaceRequested?.Invoke(this, EventArgs.Empty);
    private void OnToggleShowAll(object sender, RoutedEventArgs e)
        => ShowAllFilesChanged?.Invoke(this, ShowAllFilesItem.IsChecked);

    // Single-click vs double-click — usamos timer (~250ms) pra distinguir:
    // - MouseLeftButtonUp arma o timer (single click pendente)
    // - MouseDoubleClick cancela o timer + dispara FileActivated (edit)
    // - Timer firing sem cancel → dispara FilePreview (preview only)
    private System.Windows.Threading.DispatcherTimer? _singleClickTimer;
    private FileNode? _pendingClickNode;
    // No WPF, a ordem de eventos pra double click é:
    //   Up(1) → DoubleClick → Up(2)
    // O Up(2) chamaria o handler de single-click e rearmaria o timer (que dispararia
    // ~250ms depois trocando o modo de volta pra preview). Esta flag é setada no
    // DoubleClick e consumida no Up(2) pra ignorar esse segundo Up.
    private bool _suppressNextLeftUp;

    private void OnTreeMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_suppressNextLeftUp)
        {
            _suppressNextLeftUp = false;
            return;
        }
        if (FileTree.SelectedItem is not FileNode node) return;
        if (node.IsDirectory) return; // pasta single-click só expande/seleciona

        _pendingClickNode = node;
        if (_singleClickTimer == null)
        {
            _singleClickTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(250)
            };
            _singleClickTimer.Tick += OnSingleClickTimerTick;
        }
        _singleClickTimer.Stop();
        _singleClickTimer.Start();
    }

    private void OnSingleClickTimerTick(object? sender, EventArgs e)
    {
        _singleClickTimer?.Stop();
        var node = _pendingClickNode;
        _pendingClickNode = null;
        if (node == null) return;
        FilePreview?.Invoke(this, node.FullPath);
    }

    private void OnTreeDoubleClick(object sender, MouseButtonEventArgs e)
    {
        // Cancela single-click pendente — o user fez double-click, queremos modo edit.
        _singleClickTimer?.Stop();
        _pendingClickNode = null;
        // Suprime o próximo MouseLeftButtonUp (que viria do segundo click do double)
        // pra não rearmar o timer e voltar pra preview.
        _suppressNextLeftUp = true;

        if (FileTree.SelectedItem is FileNode node && !node.IsDirectory)
            FileActivated?.Invoke(this, node.FullPath);
    }

    // WPF TreeView não muda SelectedItem em right-click — os handlers do ContextMenu
    // dependem de SelectedItem refletir o item sob o cursor. Selecionamos manualmente
    // o TreeViewItem que está sob o mouse no momento do right-click.
    private void OnTreePreviewRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var src = e.OriginalSource as DependencyObject;
        while (src != null && src is not TreeViewItem)
        {
            src = System.Windows.Media.VisualTreeHelper.GetParent(src);
        }
        if (src is TreeViewItem item)
        {
            item.IsSelected = true;
        }
    }

    private void OnTreePreviewLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(null);
        _dragSourceNode = FindFileNode(e.OriginalSource);
    }

    private void OnTreePreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        if (_dragSourceNode == null) return;

        var diff = _dragStartPoint - e.GetPosition(null);
        if (Math.Abs(diff.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(diff.Y) < SystemParameters.MinimumVerticalDragDistance) return;

        try
        {
            var data = new DataObject(DataFormats.FileDrop, new[] { _dragSourceNode.FullPath });
            var effect = (Keyboard.Modifiers & ModifierKeys.Control) != 0
                ? DragDropEffects.Copy
                : DragDropEffects.Move;
            DragDrop.DoDragDrop(FileTree, data, effect);
        }
        finally
        {
            _dragSourceNode = null;
        }
    }

    private void OnTreeDragOver(object sender, DragEventArgs e)
    {
        e.Effects = DragDropEffects.None;
        e.Handled = true;

        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

        var target = FindFileNode(e.OriginalSource);
        if (target?.IsDirectory != true) return;

        e.Effects = (e.KeyStates & DragDropKeyStates.ControlKey) != 0
            ? DragDropEffects.Copy
            : DragDropEffects.Move;
    }

    private void OnTreeDrop(object sender, DragEventArgs e)
    {
        e.Handled = true;

        var target = FindFileNode(e.OriginalSource);
        if (target?.IsDirectory != true) return;

        if (e.Data.GetData(DataFormats.FileDrop) is not string[] sources || sources.Length == 0) return;
        var sourcePath = sources[0];

        // Cancela drops degenerados
        if (string.Equals(System.IO.Path.GetDirectoryName(sourcePath), target.FullPath, StringComparison.OrdinalIgnoreCase))
            return; // arquivo já está dentro da pasta destino
        if (string.Equals(sourcePath, target.FullPath, StringComparison.OrdinalIgnoreCase))
            return; // drop em si mesmo

        var isCopy = (e.KeyStates & DragDropKeyStates.ControlKey) != 0;

        try
        {
            string? result = isCopy
                ? _fileOps.CopyFile(sourcePath, target.FullPath, AskOverwrite)
                : _fileOps.MoveFile(sourcePath, target.FullPath, AskOverwrite);

            if (result != null) _workspace?.Refresh();
        }
        catch (FileOperationException ex)
        {
            MessageDialog.Error(Window.GetWindow(this), "Erro", ex.Message);
        }
    }

    private static FileNode? FindFileNode(object originalSource)
    {
        var dep = originalSource as System.Windows.DependencyObject;
        while (dep != null && dep is not TreeViewItem)
        {
            dep = System.Windows.Media.VisualTreeHelper.GetParent(dep);
        }
        return (dep as TreeViewItem)?.DataContext as FileNode;
    }

    private void OnRecentDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (RecentList.SelectedItem is string path)
            FileActivated?.Invoke(this, path);
    }

    private void OnOpenInNewTab(object sender, RoutedEventArgs e)
    {
        if (FileTree.SelectedItem is FileNode node && !node.IsDirectory)
            FileActivated?.Invoke(this, node.FullPath);
    }

    /// <summary>
    /// Diretório alvo de operações de criar/colar:
    /// - Item selecionado é pasta → essa pasta
    /// - Item selecionado é arquivo → diretório do arquivo
    /// - Sem seleção → root do workspace
    /// </summary>
    private string? GetTargetDirectory()
    {
        if (FileTree.SelectedItem is FileNode node)
        {
            return node.IsDirectory ? node.FullPath : System.IO.Path.GetDirectoryName(node.FullPath);
        }
        return _workspace?.RootPath;
    }

    private void OnNewFile(object sender, RoutedEventArgs e)
    {
        var targetDir = GetTargetDirectory();
        if (targetDir == null) return;

        var suggested = FileOperationsService.GenerateUniqueName(targetDir, "novo-arquivo", ".md");
        var owner = Window.GetWindow(this);
        if (owner == null) return;

        var name = InputDialog.Show(owner, "Novo arquivo",
            $"Nome do novo arquivo em '{System.IO.Path.GetFileName(targetDir)}':", suggested);
        if (string.IsNullOrWhiteSpace(name)) return;

        if (!name.EndsWith(".md", StringComparison.OrdinalIgnoreCase) &&
            !name.EndsWith(".markdown", StringComparison.OrdinalIgnoreCase))
        {
            name += ".md";
        }

        try
        {
            var baseName = System.IO.Path.GetFileNameWithoutExtension(name);
            var ext = System.IO.Path.GetExtension(name);
            var newPath = _fileOps.CreateNewFile(targetDir, baseName, ext);
            _workspace?.Refresh();
            FileActivated?.Invoke(this, newPath);
        }
        catch (FileOperationException ex)
        {
            MessageDialog.Error(owner, "Erro", ex.Message);
        }
    }

    private void OnNewFolder(object sender, RoutedEventArgs e)
    {
        var targetDir = GetTargetDirectory();
        if (targetDir == null) return;

        var suggested = FileOperationsService.GenerateUniqueName(targetDir, "nova-pasta", null);
        var owner = Window.GetWindow(this);
        if (owner == null) return;

        var name = InputDialog.Show(owner, "Nova pasta",
            $"Nome da nova pasta em '{System.IO.Path.GetFileName(targetDir)}':", suggested);
        if (string.IsNullOrWhiteSpace(name)) return;

        try
        {
            _fileOps.CreateNewFolder(targetDir, name);
            _workspace?.Refresh();
        }
        catch (FileOperationException ex)
        {
            MessageDialog.Error(owner, "Erro", ex.Message);
        }
    }

    private const string CutFlagFormat = "Preferred DropEffect";

    private void OnCopy(object sender, RoutedEventArgs e)
    {
        if (FileTree.SelectedItem is not FileNode node) return;
        SetClipboardForFile(node.FullPath, cut: false);
    }

    private void OnCut(object sender, RoutedEventArgs e)
    {
        if (FileTree.SelectedItem is not FileNode node) return;
        SetClipboardForFile(node.FullPath, cut: true);
    }

    private static void SetClipboardForFile(string path, bool cut)
    {
        try
        {
            var data = new DataObject();
            data.SetData(DataFormats.FileDrop, new[] { path });
            data.SetData(DataFormats.Text, path);
            if (cut)
            {
                // DROPEFFECT_MOVE = 2 — compatível com Windows Explorer.
                var flag = new byte[] { 2, 0, 0, 0 };
                data.SetData(CutFlagFormat, new MemoryStream(flag));
            }
            Clipboard.SetDataObject(data, copy: true);
        }
        catch (Exception ex)
        {
            MessageDialog.Error(Application.Current?.MainWindow,
                "Erro", $"Erro ao acessar clipboard:\n{ex.Message}");
        }
    }

    private void OnPaste(object sender, RoutedEventArgs e)
    {
        var targetDir = GetTargetDirectory();
        if (targetDir == null) return;

        try
        {
            var data = Clipboard.GetDataObject();
            if (data?.GetDataPresent(DataFormats.FileDrop) != true) return;

            var paths = (string[])data.GetData(DataFormats.FileDrop);
            if (paths == null || paths.Length == 0) return;
            var sourcePath = paths[0]; // YAGNI: 1 item por vez

            // Suporta tanto arquivos quanto pastas.
            bool sourceIsFile = File.Exists(sourcePath);
            bool sourceIsDir = Directory.Exists(sourcePath);
            if (!sourceIsFile && !sourceIsDir)
            {
                MessageDialog.Info(Window.GetWindow(this), "Erro",
                    "Arquivo/pasta do clipboard não existe mais.");
                return;
            }

            // Detectar Cut vs Copy pela flag.
            bool isCut = false;
            if (data.GetDataPresent(CutFlagFormat) &&
                data.GetData(CutFlagFormat) is MemoryStream ms)
            {
                var bytes = ms.ToArray();
                isCut = bytes.Length > 0 && bytes[0] == 2;
            }

            // _fileOps.CopyFile/MoveFile lidam apenas com arquivos por enquanto.
            // Pasta no clipboard: avisar e abortar (YAGNI até v2).
            if (sourceIsDir)
            {
                MessageDialog.Info(Window.GetWindow(this), "Não suportado",
                    "Copiar/colar pastas ainda não é suportado (apenas arquivos).\n\nVocê pode arrastar a pasta no Explorer do Windows.");
                return;
            }

            string? result = isCut
                ? _fileOps.MoveFile(sourcePath, targetDir, AskOverwrite)
                : _fileOps.CopyFile(sourcePath, targetDir, AskOverwrite);

            if (result != null)
            {
                _workspace?.Refresh();
                if (isCut) Clipboard.Clear();
            }
        }
        catch (FileOperationException ex)
        {
            MessageDialog.Error(Window.GetWindow(this), "Erro", ex.Message);
        }
        catch (Exception ex)
        {
            MessageDialog.Error(Window.GetWindow(this), "Erro",
                $"Erro inesperado ao colar:\n{ex.Message}");
        }
    }

    private bool AskOverwrite(string existingPath)
    {
        var name = Path.GetFileName(existingPath);
        return MessageDialog.Confirm(Window.GetWindow(this), "Confirmar sobrescrita",
            $"'{name}' já existe no destino.\n\nSobrescrever?", MessageDialogKind.Warning);
    }

    private void OnRename(object sender, RoutedEventArgs e)
    {
        if (FileTree.SelectedItem is not FileNode node) return;
        var owner = Window.GetWindow(this);
        if (owner == null) return;

        var newName = InputDialog.Show(owner, "Renomear", $"Novo nome para '{node.Name}':", node.Name);
        if (string.IsNullOrWhiteSpace(newName) || newName == node.Name) return;

        var newPath = Path.Combine(Path.GetDirectoryName(node.FullPath) ?? "", newName);
        try
        {
            if (node.IsDirectory) Directory.Move(node.FullPath, newPath);
            else File.Move(node.FullPath, newPath);
        }
        catch (Exception ex)
        {
            MessageDialog.Error(owner, "Erro", $"Erro ao renomear:\n{ex.Message}");
        }
    }

    private void OnDelete(object sender, RoutedEventArgs e)
    {
        if (FileTree.SelectedItem is not FileNode node) return;
        var owner = Window.GetWindow(this);
        var confirmed = MessageDialog.Confirm(owner, "Confirmar exclusão",
            $"Mover '{node.Name}' para a Lixeira?", MessageDialogKind.Warning);
        if (!confirmed) return;
        try
        {
            // Manda pra Lixeira do Windows (em vez de File.Delete que é permanente).
            // Usa Microsoft.VisualBasic.FileIO — built-in no BCL, sem NuGet.
            if (node.IsDirectory)
            {
                Microsoft.VisualBasic.FileIO.FileSystem.DeleteDirectory(
                    node.FullPath,
                    Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                    Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
            }
            else
            {
                Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(
                    node.FullPath,
                    Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                    Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
            }
        }
        catch (Exception ex)
        {
            MessageDialog.Error(owner, "Erro", $"Erro ao excluir:\n{ex.Message}");
        }
    }

    private void OnRevealInExplorer(object sender, RoutedEventArgs e)
    {
        if (FileTree.SelectedItem is not FileNode node) return;
        try
        {
            Process.Start("explorer.exe", $"/select,\"{node.FullPath}\"");
        }
        catch { }
    }

    private void OnCopyPath(object sender, RoutedEventArgs e)
    {
        if (FileTree.SelectedItem is not FileNode node) return;
        try { Clipboard.SetText(node.FullPath); } catch { }
    }
}
