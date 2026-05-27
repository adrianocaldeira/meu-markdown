using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace MeuMarkdown.Services;

/// <summary>
/// Resultado do registro da associação de arquivos .md.
/// </summary>
public enum FileAssociationResult
{
    /// <summary>Associação registrada e nenhum "UserChoice" do Windows está bloqueando.</summary>
    Success,

    /// <summary>
    /// Associação registrada, mas o Windows mantém um "UserChoice" protegido apontando para
    /// outro aplicativo. O usuário precisa definir o padrão pela via nativa do Explorer.
    /// </summary>
    BlockedByUserChoice,
}

public class FileAssociationService
{
    private const string ProgId = "MeuMarkdown.md";

    private const string UserChoiceKeyPath =
        @"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.md\UserChoice";

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

    /// <summary>
    /// Registra o MeuMarkdown como handler de arquivos .md em HKCU\Software\Classes e tenta
    /// remover o "UserChoice" do Windows, que tem prioridade sobre o ProgId e faz o .md continuar
    /// abrindo em outro editor mesmo após o registro.
    /// </summary>
    /// <returns>
    /// <see cref="FileAssociationResult.Success"/> se a associação ficou efetiva, ou
    /// <see cref="FileAssociationResult.BlockedByUserChoice"/> se o Windows mantém um UserChoice
    /// protegido para outro aplicativo (exige ação manual do usuário).
    /// </returns>
    public FileAssociationResult Register()
    {
        var exePath = Environment.ProcessPath
            ?? throw new InvalidOperationException("Não foi possível determinar o caminho do executável.");

        if (!exePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "A associação de arquivos só pode ser registrada ao executar o MeuMarkdown.exe publicado, não via 'dotnet run'.");


        using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Classes\.md"))
            key.SetValue("", ProgId);

        using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Classes\.md\OpenWithProgids"))
            key.SetValue(ProgId, "");

        using (var key = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{ProgId}"))
            key.SetValue("", "Arquivo Markdown");

        using (var key = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{ProgId}\DefaultIcon"))
            key.SetValue("", $"\"{exePath}\",0");

        using (var key = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{ProgId}\shell\open\command"))
            key.SetValue("", $"\"{exePath}\" \"%1\"");

        var result = ClearConflictingUserChoice();

        SHChangeNotify(0x08000000, 0x0000, IntPtr.Zero, IntPtr.Zero);

        return result;
    }

    /// <summary>
    /// Remove a subchave "UserChoice" de FileExts\.md quando ela aponta para outro aplicativo.
    /// No Windows 10/11 essa chave costuma ter ACL protegida contra escrita/exclusão para impedir
    /// sequestro de associação; nesse caso a exclusão falha e o método sinaliza bloqueio.
    /// </summary>
    private static FileAssociationResult ClearConflictingUserChoice()
    {
        try
        {
            using var userChoice = Registry.CurrentUser.OpenSubKey(UserChoiceKeyPath);
            if (userChoice is null)
                return FileAssociationResult.Success; // nenhum UserChoice: o ProgId já vale

            if (userChoice.GetValue("ProgId") is string current &&
                current.Equals(ProgId, StringComparison.OrdinalIgnoreCase))
                return FileAssociationResult.Success; // já somos o escolhido
        }
        catch (Exception)
        {
            // Falha ao ler: trata como bloqueio para orientar o usuário pela via nativa.
            return FileAssociationResult.BlockedByUserChoice;
        }

        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(UserChoiceKeyPath, throwOnMissingSubKey: false);
        }
        catch (UnauthorizedAccessException)
        {
            return FileAssociationResult.BlockedByUserChoice; // ACL protegida (Win10/11)
        }

        // Confirma que o UserChoice conflitante realmente saiu.
        using var recheck = Registry.CurrentUser.OpenSubKey(UserChoiceKeyPath);
        if (recheck is null)
            return FileAssociationResult.Success;

        return recheck.GetValue("ProgId") is string p &&
               p.Equals(ProgId, StringComparison.OrdinalIgnoreCase)
            ? FileAssociationResult.Success
            : FileAssociationResult.BlockedByUserChoice;
    }
}
