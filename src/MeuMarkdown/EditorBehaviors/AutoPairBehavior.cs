using ICSharpCode.AvalonEdit;

namespace MeuMarkdown.EditorBehaviors;

public static class AutoPairBehavior
{
    public static void Attach(TextEditor editor)
    {
        editor.TextArea.TextEntering += (s, e) =>
        {
            if (string.IsNullOrEmpty(e.Text)) return;

            var doc = editor.Document;
            var caretOffset = editor.CaretOffset;
            var fullText = doc.Text;

            if (FencedCodeDetector.IsInsideFencedCode(fullText, caretOffset)) return;

            // Skip-on-duplicate
            if (caretOffset < fullText.Length)
            {
                var nextChar = fullText[caretOffset].ToString();
                if (e.Text == nextChar && IsClosingChar(nextChar))
                {
                    editor.CaretOffset = caretOffset + 1;
                    e.Handled = true;
                    return;
                }
            }

            // Selection-wrapping
            if (editor.SelectionLength > 0)
            {
                var closing = AutoPair.GetClosing(e.Text) ?? (e.Text == "_" ? "_" : null);
                if (closing == null) return;
                var selStart = editor.SelectionStart;
                var selText = editor.SelectedText;
                doc.Replace(selStart, selText.Length, e.Text + selText + closing);
                editor.Select(selStart + e.Text.Length, selText.Length);
                e.Handled = true;
                return;
            }

            // Auto-pair simples
            var pair = AutoPair.GetClosing(e.Text);
            if (pair != null)
            {
                doc.Insert(caretOffset, e.Text + pair);
                editor.CaretOffset = caretOffset + e.Text.Length;
                e.Handled = true;
                return;
            }

            // Underscore: só auto-pair em início de palavra
            if (e.Text == "_")
            {
                var caretLine = doc.GetLineByOffset(caretOffset);
                var lineStart = caretLine.Offset;
                var lineBefore = doc.GetText(lineStart, caretOffset - lineStart);
                if (AutoPair.ShouldAutoPair("_", lineBefore, lineBefore.Length))
                {
                    doc.Insert(caretOffset, "__");
                    editor.CaretOffset = caretOffset + 1;
                    e.Handled = true;
                }
            }
        };
    }

    private static bool IsClosingChar(string c) => c is ")" or "]" or "`";
}
