using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace MeuMarkdown.Services;

public class FileAssociationService
{
    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

    public void Register()
    {
        var exePath = Environment.ProcessPath
            ?? throw new InvalidOperationException("Não foi possível determinar o caminho do executável.");

        if (!exePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "A associação de arquivos só pode ser registrada ao executar o MeuMarkdown.exe publicado, não via 'dotnet run'.");


        using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Classes\.md"))
            key.SetValue("", "MeuMarkdown.md");

        using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Classes\.md\OpenWithProgids"))
            key.SetValue("MeuMarkdown.md", "");

        using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Classes\MeuMarkdown.md"))
            key.SetValue("", "Arquivo Markdown");

        using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Classes\MeuMarkdown.md\DefaultIcon"))
            key.SetValue("", $"\"{exePath}\",0");

        using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Classes\MeuMarkdown.md\shell\open\command"))
            key.SetValue("", $"\"{exePath}\" \"%1\"");

        SHChangeNotify(0x08000000, 0x0000, IntPtr.Zero, IntPtr.Zero);
    }
}
