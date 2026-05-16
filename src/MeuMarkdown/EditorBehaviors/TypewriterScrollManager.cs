using ICSharpCode.AvalonEdit;

namespace MeuMarkdown.EditorBehaviors;

public class TypewriterScrollManager
{
    private readonly TextEditor _editor;
    private bool _enabled;

    public TypewriterScrollManager(TextEditor editor)
    {
        _editor = editor;
    }

    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (_enabled == value) return;
            _enabled = value;
            if (_enabled)
            {
                _editor.TextArea.Caret.PositionChanged += OnCaretChanged;
                CenterCaret();
            }
            else
            {
                _editor.TextArea.Caret.PositionChanged -= OnCaretChanged;
            }
        }
    }

    private void OnCaretChanged(object? sender, EventArgs e) => CenterCaret();

    private void CenterCaret()
    {
        if (!_enabled) return;
        var textView = _editor.TextArea.TextView;
        if (!textView.IsLoaded || textView.ActualHeight <= 0) return;

        var caretRect = _editor.TextArea.Caret.CalculateCaretRectangle();
        var viewportHeight = textView.ActualHeight;
        var currentY = textView.VerticalOffset;
        var caretY = caretRect.Top;
        var targetY = caretY - viewportHeight / 2;
        if (Math.Abs(targetY - currentY) > 1)
        {
            _editor.ScrollToVerticalOffset(targetY);
        }
    }
}
