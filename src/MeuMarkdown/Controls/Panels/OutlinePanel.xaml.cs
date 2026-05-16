using System.Collections.Specialized;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using MeuMarkdown.Models;
using MeuMarkdown.ViewModels;

namespace MeuMarkdown.Controls.Panels;

public partial class OutlinePanel : UserControl
{
    public event EventHandler<Heading>? HeadingSelected;

    private DocumentTabViewModel? _currentTab;

    public OutlinePanel()
    {
        InitializeComponent();
    }

    public void BindToTab(DocumentTabViewModel? tab)
    {
        if (_currentTab != null)
            ((INotifyCollectionChanged)_currentTab.Headings).CollectionChanged -= OnHeadingsChanged;

        _currentTab = tab;
        HeadingsList.ItemsSource = tab?.Headings;
        UpdateEmptyState();

        if (tab != null)
            ((INotifyCollectionChanged)tab.Headings).CollectionChanged += OnHeadingsChanged;
    }

    public void HighlightCurrentHeading(Heading? heading)
    {
        if (heading == null || _currentTab == null)
        {
            HeadingsList.SelectedItem = null;
            return;
        }
        HeadingsList.SelectedItem = heading;
        HeadingsList.ScrollIntoView(heading);
    }

    private void OnHeadingsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        UpdateEmptyState();
    }

    private void UpdateEmptyState()
    {
        var count = _currentTab?.Headings.Count ?? 0;
        EmptyState.Visibility = count == 0 ? Visibility.Visible : Visibility.Collapsed;
        HeadingsList.Visibility = count == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private void OnHeadingClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (HeadingsList.SelectedItem is Heading h)
            HeadingSelected?.Invoke(this, h);
    }
}

public class IndentToMarginConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int indent)
            return new Thickness(indent, 0, 0, 0);
        return new Thickness(0);
    }

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
