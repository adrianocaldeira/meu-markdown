using System.Windows;
using System.Windows.Controls;
using MeuMarkdown.Models.Mermaid;
using MeuMarkdown.ViewModels;

namespace MeuMarkdown.Controls.MermaidBuilderPanels;

public partial class FlowchartBuilderPanel : UserControl
{
    public FlowchartBuilderPanel()
    {
        InitializeComponent();
        Loaded += OnLoadedSelectFirst;
    }

    private MermaidBuilderViewModel? Vm => DataContext as MermaidBuilderViewModel;

    private void OnLoadedSelectFirst(object sender, RoutedEventArgs e)
    {
        if (Vm == null) return;
        if (NodesList.SelectedItem == null && Vm.Flowchart.Nodes.Count > 0)
            NodesList.SelectedItem = Vm.Flowchart.Nodes[0];
        if (EdgesList.SelectedItem == null && Vm.Flowchart.Edges.Count > 0)
            EdgesList.SelectedItem = Vm.Flowchart.Edges[0];
    }

    private void OnAddNode(object sender, RoutedEventArgs e)
    {
        if (Vm == null) return;
        var existingIds = Vm.Flowchart.Nodes.Select(n => n.Id).ToHashSet();
        var i = 1;
        while (existingIds.Contains($"n{i}")) i++;
        var node = new FlowNode { Id = $"n{i}", Label = "Novo nó", Shape = FlowNodeShape.Rectangle };
        Vm.Flowchart.Nodes.Add(node);
        NodesList.SelectedItem = node;
    }

    private void OnRemoveNode(object sender, RoutedEventArgs e)
    {
        if (Vm == null) return;
        if (NodesList.SelectedItem is not FlowNode node) return;
        Vm.Flowchart.Nodes.Remove(node);
    }

    private void OnMoveNodeUp(object sender, RoutedEventArgs e) => Move(NodesList, Vm?.Flowchart.Nodes, -1);
    private void OnMoveNodeDown(object sender, RoutedEventArgs e) => Move(NodesList, Vm?.Flowchart.Nodes, +1);

    private void OnAddEdge(object sender, RoutedEventArgs e)
    {
        if (Vm == null) return;
        if (Vm.Flowchart.Nodes.Count < 2) return;
        var edge = new FlowEdge
        {
            FromId = Vm.Flowchart.Nodes[0].Id,
            ToId = Vm.Flowchart.Nodes[1].Id,
            Arrow = FlowArrowType.Solid,
        };
        Vm.Flowchart.Edges.Add(edge);
        EdgesList.SelectedItem = edge;
    }

    private void OnRemoveEdge(object sender, RoutedEventArgs e)
    {
        if (Vm == null) return;
        if (EdgesList.SelectedItem is not FlowEdge edge) return;
        Vm.Flowchart.Edges.Remove(edge);
    }

    private static void Move<T>(ListBox list, System.Collections.ObjectModel.ObservableCollection<T>? coll, int delta)
    {
        if (coll == null) return;
        if (list.SelectedItem is not T item) return;
        var idx = coll.IndexOf(item);
        var newIdx = idx + delta;
        if (newIdx < 0 || newIdx >= coll.Count) return;
        coll.Move(idx, newIdx);
        list.SelectedItem = item;
    }
}
