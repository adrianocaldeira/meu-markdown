using CommunityToolkit.Mvvm.ComponentModel;

namespace MeuMarkdown.Models.Mermaid;

public partial class Actor : ObservableObject
{
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _alias = "";
    [ObservableProperty] private bool _isActor;
}
