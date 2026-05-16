using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using ICSharpCode.AvalonEdit;
using MeuMarkdown.ViewModels;

namespace MeuMarkdown.EditorBehaviors;

public static class ImagePasteHandler
{
    public static void Attach(TextEditor editor, Func<DocumentTabViewModel?> getSelectedTab)
    {
        DataObject.AddPastingHandler(editor, (sender, e) =>
        {
            if (!Clipboard.ContainsImage()) return;

            var tab = getSelectedTab();
            if (tab == null || string.IsNullOrEmpty(tab.FilePath))
            {
                MessageBox.Show(
                    "Salve o documento antes de colar imagens (o arquivo precisa estar em uma pasta).",
                    "Documento não salvo", MessageBoxButton.OK, MessageBoxImage.Information);
                e.CancelCommand();
                return;
            }

            try
            {
                var image = Clipboard.GetImage();
                if (image == null) return;

                var dir = Path.GetDirectoryName(tab.FilePath)!;
                var assetsDir = Path.Combine(dir, "assets");
                Directory.CreateDirectory(assetsDir);

                var filename = $"clipboard-{DateTime.Now:yyyyMMdd-HHmmss}.png";
                var fullPath = Path.Combine(assetsDir, filename);

                using (var stream = File.Create(fullPath))
                {
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(image));
                    encoder.Save(stream);
                }

                var altName = Path.GetFileNameWithoutExtension(filename);
                var markdown = $"![{altName}](./assets/{filename})";
                editor.Document.Insert(editor.CaretOffset, markdown);
                editor.CaretOffset += markdown.Length;
                e.CancelCommand();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao salvar imagem do clipboard:\n{ex.Message}",
                    "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                e.CancelCommand();
            }
        });
    }
}
