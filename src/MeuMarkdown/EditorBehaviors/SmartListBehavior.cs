using System.Windows.Input;
using ICSharpCode.AvalonEdit;

namespace MeuMarkdown.EditorBehaviors;

public static class SmartListBehavior
{
    public static void Attach(TextEditor editor)
    {
        editor.PreviewKeyDown += (s, e) =>
        {
            if (e.Key != Key.Enter || Keyboard.Modifiers != ModifierKeys.None) return;

            var doc = editor.Document;
            var caretLine = doc.GetLineByOffset(editor.CaretOffset);
            var currentLine = doc.GetText(caretLine.Offset, caretLine.Length);

            var prefix = ListContinuation.Compute(currentLine);
            if (prefix == "") return;

            if (prefix == null)
            {
                // Marcador vazio: remove a linha (vai virar quebra normal sem marcador)
                doc.Replace(caretLine.Offset, caretLine.Length, "");
                return;
            }

            var insertion = "\n" + prefix;
            doc.Insert(editor.CaretOffset, insertion);
            editor.CaretOffset = caretLine.Offset + caretLine.Length + insertion.Length;
            e.Handled = true;
        };
    }
}
