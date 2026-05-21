using System.Windows;
using System.Windows.Controls;
using MeuMarkdown.Models.Mermaid;
using MeuMarkdown.ViewModels;

namespace MeuMarkdown.Controls.MermaidBuilderPanels;

public partial class SequenceBuilderPanel : UserControl
{
    public SequenceBuilderPanel()
    {
        InitializeComponent();
        Loaded += OnLoadedSelectFirst;
    }

    private MermaidBuilderViewModel? Vm => DataContext as MermaidBuilderViewModel;

    private void OnLoadedSelectFirst(object sender, RoutedEventArgs e)
    {
        if (Vm == null) return;
        if (ActorsList.SelectedItem == null && Vm.Sequence.Actors.Count > 0)
            ActorsList.SelectedItem = Vm.Sequence.Actors[0];
        if (MessagesList.SelectedItem == null && Vm.Sequence.Messages.Count > 0)
            MessagesList.SelectedItem = Vm.Sequence.Messages[0];
    }

    private void OnAddActor(object sender, RoutedEventArgs e)
    {
        if (Vm == null) return;
        var existingNames = Vm.Sequence.Actors.Select(a => a.Name).ToHashSet();
        var i = Vm.Sequence.Actors.Count + 1;
        while (existingNames.Contains($"Actor{i}")) i++;
        var actor = new Actor { Name = $"Actor{i}", IsActor = false };
        Vm.Sequence.Actors.Add(actor);
        ActorsList.SelectedItem = actor;
    }

    private void OnRemoveActor(object sender, RoutedEventArgs e)
    {
        if (Vm == null) return;
        if (ActorsList.SelectedItem is not Actor actor) return;
        Vm.Sequence.Actors.Remove(actor);
    }

    private void OnMoveActorUp(object sender, RoutedEventArgs e) => Move(ActorsList, Vm?.Sequence.Actors, -1);
    private void OnMoveActorDown(object sender, RoutedEventArgs e) => Move(ActorsList, Vm?.Sequence.Actors, +1);

    private void OnAddMessage(object sender, RoutedEventArgs e)
    {
        if (Vm == null) return;
        if (Vm.Sequence.Actors.Count < 1) return;
        var first = Vm.Sequence.Actors[0].Name;
        var second = Vm.Sequence.Actors.Count > 1 ? Vm.Sequence.Actors[1].Name : first;
        var msg = new Message { FromActor = first, ToActor = second, Arrow = SequenceArrowType.Sync, Label = "" };
        Vm.Sequence.Messages.Add(msg);
        MessagesList.SelectedItem = msg;
    }

    private void OnRemoveMessage(object sender, RoutedEventArgs e)
    {
        if (Vm == null) return;
        if (MessagesList.SelectedItem is not Message msg) return;
        Vm.Sequence.Messages.Remove(msg);
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
