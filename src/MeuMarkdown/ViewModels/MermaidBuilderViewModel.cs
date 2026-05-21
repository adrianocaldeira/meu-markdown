using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeuMarkdown.Models.Mermaid;

namespace MeuMarkdown.ViewModels;

public enum BuilderDiagramKind { Flowchart, Sequence }

public partial class MermaidBuilderViewModel : ObservableObject
{
    private readonly DispatcherTimer _debounce;

    [ObservableProperty] private BuilderDiagramKind _diagramKind = BuilderDiagramKind.Flowchart;
    [ObservableProperty] private FlowchartModel _flowchart = new();
    [ObservableProperty] private SequenceModel _sequence = new();
    [ObservableProperty] private string _generatedCode = "";
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private bool _isModified;

    /// <summary>
    /// Disparado quando o modelo muda e o debounce já expirou.
    /// Janela escuta isso para chamar MermaidPreviewLite.RenderAsync.
    /// </summary>
    public event Action<string>? RenderRequested;

    public MermaidBuilderViewModel()
    {
        _debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _debounce.Tick += OnDebounceTick;

        SeedFlowchart();
        SeedSequence();
        AttachAllListeners();
        RegenerateImmediately();
    }

    private void SeedFlowchart()
    {
        Flowchart.Nodes.Add(new FlowNode { Id = "n1", Label = "Início", Shape = FlowNodeShape.Rectangle });
        Flowchart.Nodes.Add(new FlowNode { Id = "n2", Label = "Fim", Shape = FlowNodeShape.Rectangle });
        Flowchart.Edges.Add(new FlowEdge { FromId = "n1", ToId = "n2", Arrow = FlowArrowType.Solid });
    }

    private void SeedSequence()
    {
        Sequence.Actors.Add(new Actor { Name = "Alice", IsActor = true });
        Sequence.Actors.Add(new Actor { Name = "Bob", IsActor = false });
        Sequence.Messages.Add(new Message { FromActor = "Alice", ToActor = "Bob", Arrow = SequenceArrowType.Sync, Label = "Olá" });
    }

    private void AttachAllListeners()
    {
        Flowchart.PropertyChanged += OnModelChanged;
        Sequence.PropertyChanged += OnModelChanged;
        AttachCollection(Flowchart.Nodes);
        AttachCollection(Flowchart.Edges);
        AttachCollection(Sequence.Actors);
        AttachCollection(Sequence.Messages);
    }

    private void AttachCollection<T>(ObservableCollection<T> collection) where T : INotifyPropertyChanged
    {
        collection.CollectionChanged += OnCollectionChanged;
        foreach (var item in collection)
            item.PropertyChanged += OnItemChanged;
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
            foreach (INotifyPropertyChanged item in e.NewItems)
                item.PropertyChanged += OnItemChanged;
        if (e.OldItems != null)
            foreach (INotifyPropertyChanged item in e.OldItems)
                item.PropertyChanged -= OnItemChanged;
        OnModelChanged(sender, new PropertyChangedEventArgs("Collection"));
    }

    private void OnItemChanged(object? sender, PropertyChangedEventArgs e) => OnModelChanged(sender, e);

    private void OnModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        IsModified = true;
        _debounce.Stop();
        _debounce.Start();
    }

    private void OnDebounceTick(object? sender, EventArgs e)
    {
        _debounce.Stop();
        RegenerateImmediately();
    }

    public void RegenerateImmediately()
    {
        GeneratedCode = DiagramKind switch
        {
            BuilderDiagramKind.Flowchart => Flowchart.ToMermaid(),
            BuilderDiagramKind.Sequence => Sequence.ToMermaid(),
            _ => "",
        };
        RenderRequested?.Invoke(GeneratedCode);
    }

    partial void OnDiagramKindChanged(BuilderDiagramKind value)
    {
        RegenerateImmediately();
    }

    /// <summary>
    /// Chamado pelo MermaidPreviewLite quando recebe erro de render.
    /// </summary>
    public void SetRenderError(string? message)
    {
        ErrorMessage = string.IsNullOrEmpty(message) ? null : "⚠ " + message;
    }
}
