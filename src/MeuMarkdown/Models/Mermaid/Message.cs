using CommunityToolkit.Mvvm.ComponentModel;

namespace MeuMarkdown.Models.Mermaid;

public partial class Message : ObservableObject
{
    [ObservableProperty] private string _fromActor = "";
    [ObservableProperty] private string _toActor = "";
    [ObservableProperty] private SequenceArrowType _arrow = SequenceArrowType.Sync;
    [ObservableProperty] private string _label = "";
}
