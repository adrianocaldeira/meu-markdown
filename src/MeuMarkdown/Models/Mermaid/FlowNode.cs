using CommunityToolkit.Mvvm.ComponentModel;

namespace MeuMarkdown.Models.Mermaid;

public partial class FlowNode : ObservableObject
{
    [ObservableProperty] private string _id = "";
    [ObservableProperty] private string _label = "";
    [ObservableProperty] private FlowNodeShape _shape = FlowNodeShape.Rectangle;
}
