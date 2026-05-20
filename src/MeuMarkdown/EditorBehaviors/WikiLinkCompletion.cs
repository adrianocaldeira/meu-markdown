using System.IO;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using MeuMarkdown.Services;

namespace MeuMarkdown.EditorBehaviors;

/// <summary>
/// Autocomplete de wiki-links no AvalonEdit. Quando o usuário digita o segundo
/// '[' (formando '[[' no offset corrente), abre um CompletionWindow listando
/// arquivos .md do workspace.
/// </summary>
public static class WikiLinkCompletion
{
    private const int MaxItems = 50;

    public static void Attach(TextEditor editor, Func<WorkspaceService?> getWorkspace)
    {
        editor.TextArea.TextEntered += (sender, e) =>
        {
            if (e.Text != "[") return;
            var offset = editor.CaretOffset;
            if (offset < 2) return;
            var prevChar = editor.Document.GetCharAt(offset - 2);
            if (prevChar != '[') return;
            if (IsInsideFencedCode(editor.Document, offset)) return;

            var workspace = getWorkspace();
            if (workspace?.RootPath == null) return;

            var window = new CompletionWindow(editor.TextArea);
            var data = window.CompletionList.CompletionData;

            var files = workspace.EnumerateMarkdownFiles()
                .OrderBy(p => Path.GetFileName(p), StringComparer.OrdinalIgnoreCase)
                .Take(MaxItems);

            foreach (var path in files)
            {
                var name = Path.GetFileNameWithoutExtension(path);
                var rel = Path.GetRelativePath(workspace.RootPath, path);
                data.Add(new WikiLinkCompletionData(name, rel));
            }

            if (data.Count == 0) return;
            window.Show();
        };
    }

    private static bool IsInsideFencedCode(TextDocument document, int offset)
    {
        var text = document.GetText(0, offset);
        var count = 0;
        var lines = text.Split('\n');
        foreach (var line in lines)
        {
            if (line.TrimStart().StartsWith("```")) count++;
        }
        return count % 2 == 1;
    }

    private class WikiLinkCompletionData : ICompletionData
    {
        public WikiLinkCompletionData(string text, string description)
        {
            Text = text;
            Description = description;
        }

        public System.Windows.Media.ImageSource? Image => null;
        public string Text { get; }
        public object Content => Text;
        public object Description { get; }
        public double Priority => 1.0;

        public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
        {
            textArea.Document.Replace(completionSegment, Text + "]]");
        }
    }
}
