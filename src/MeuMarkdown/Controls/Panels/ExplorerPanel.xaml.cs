using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MeuMarkdown.Models;
using MeuMarkdown.Services;

namespace MeuMarkdown.Controls.Panels;

public partial class ExplorerPanel : UserControl
{
    private WorkspaceService? _workspace;
    private RecentFilesService? _recent;

    public event EventHandler<string>? FileActivated;
    public event EventHandler? OpenFolderRequested;
    public event EventHandler? CloseWorkspaceRequested;
    public event EventHandler<bool>? ShowAllFilesChanged;
    public event EventHandler<FileTreeSortMode>? SortModeChanged;

    public ExplorerPanel()
    {
        InitializeComponent();
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

    private void OnTreeDoubleClick(object sender, MouseButtonEventArgs e)
    {
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

    private void OnRename(object sender, RoutedEventArgs e)
    {
        if (FileTree.SelectedItem is not FileNode node) return;
        var dlg = new Window
        {
            Title = "Renomear",
            Width = 360, Height = 130, ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = Window.GetWindow(this)
        };
        var grid = new Grid { Margin = new Thickness(12) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var tb = new TextBox { Text = node.Name, FontSize = 13, Margin = new Thickness(0, 0, 0, 12) };
        var ok = new Button { Content = "OK", Width = 80, IsDefault = true, HorizontalAlignment = HorizontalAlignment.Right };
        Grid.SetRow(tb, 0);
        Grid.SetRow(ok, 1);
        grid.Children.Add(tb);
        grid.Children.Add(ok);
        dlg.Content = grid;
        ok.Click += (_, _) => dlg.DialogResult = true;
        tb.Focus();
        tb.SelectAll();
        if (dlg.ShowDialog() != true) return;

        var newName = tb.Text.Trim();
        if (string.IsNullOrEmpty(newName) || newName == node.Name) return;
        var newPath = Path.Combine(Path.GetDirectoryName(node.FullPath) ?? "", newName);
        try
        {
            if (node.IsDirectory) Directory.Move(node.FullPath, newPath);
            else File.Move(node.FullPath, newPath);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao renomear:\n{ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnDelete(object sender, RoutedEventArgs e)
    {
        if (FileTree.SelectedItem is not FileNode node) return;
        var result = MessageBox.Show(
            $"Excluir '{node.Name}' permanentemente?\n\n(Nota: v1 não suporta lixeira)",
            "Confirmar exclusão", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes) return;
        try
        {
            if (node.IsDirectory) Directory.Delete(node.FullPath, recursive: true);
            else File.Delete(node.FullPath);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao excluir:\n{ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
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
