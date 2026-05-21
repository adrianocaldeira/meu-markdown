using CommunityToolkit.Mvvm.ComponentModel;

namespace MeuMarkdown.Models.Mermaid;

public partial class FlowEdge : ObservableObject
{
    [ObservableProperty] private string _fromId = "";
    [ObservableProperty] private string _toId = "";
    [ObservableProperty] private FlowArrowType _arrow = FlowArrowType.Solid;
    [ObservableProperty] private string _label = "";
}
