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
                // Marcador vazio: remove o marcador da linha atual e insere quebra normal.
                // Implementação explícita pra não depender do fallthrough do PreviewKeyDown.
                doc.Replace(caretLine.Offset, caretLine.Length, "");
                doc.Insert(caretLine.Offset, "\n");
                editor.CaretOffset = caretLine.Offset + 1;
                e.Handled = true;
                return;
            }

            // Insere "\n<prefix>" no cursor
            var lineStartBefore = caretLine.Offset;
            var lineLengthBefore = caretLine.Length;
            var insertion = "\n" + prefix;
            doc.Insert(editor.CaretOffset, insertion);
            editor.CaretOffset = lineStartBefore + lineLengthBefore + insertion.Length;
            e.Handled = true;
        };
    }
}
