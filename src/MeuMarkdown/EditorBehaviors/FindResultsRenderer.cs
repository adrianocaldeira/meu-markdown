using System.Windows;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Rendering;
using MeuMarkdown.Services;

namespace MeuMarkdown.EditorBehaviors;

public class FindResultsRenderer : IBackgroundRenderer
{
    private static readonly SolidColorBrush _matchBrush = new(Color.FromArgb(80, 0xFF, 0xFF, 0x00));
    private static readonly SolidColorBrush _activeBrush = new(Color.FromArgb(140, 0xFF, 0xA5, 0x00));

    static FindResultsRenderer()
    {
        _matchBrush.Freeze();
        _activeBrush.Freeze();
    }

    public IReadOnlyList<SearchMatch> Matches { get; set; } = Array.Empty<SearchMatch>();
    public int ActiveMatchIndex { get; set; } = -1;

    public KnownLayer Layer => KnownLayer.Background;

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (textView == null || drawingContext == null || Matches.Count == 0) return;
        textView.EnsureVisualLines();

        for (int i = 0; i < Matches.Count; i++)
        {
            var match = Matches[i];
            var brush = i == ActiveMatchIndex ? _activeBrush : _matchBrush;

            var segment = new ICSharpCode.AvalonEdit.Document.TextSegment
            {
                StartOffset = match.Start,
                Length = match.Length
            };

            foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, segment))
            {
                drawingContext.DrawRectangle(brush, null,
                    new Rect(rect.Location, new Size(rect.Width, rect.Height)));
            }
        }
    }
}
