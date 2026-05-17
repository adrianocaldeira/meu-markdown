using System.Reflection;

namespace MeuMarkdown;

/// <summary>
/// Centraliza acesso à versão do app — sempre lida do assembly em runtime.
/// Fonte da versão: <c>Directory.Build.props</c> na raiz do repo.
/// </summary>
public static class VersionInfo
{
    /// <summary>
    /// Versão semântica no formato "X.Y.Z" (ex: "1.2.0").
    /// </summary>
    public static string Current { get; } = ResolveCurrent();

    private static string ResolveCurrent()
    {
        var asm = Assembly.GetExecutingAssembly();

        var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        if (info != null && !string.IsNullOrWhiteSpace(info.InformationalVersion))
        {
            var v = info.InformationalVersion;
            var plus = v.IndexOf('+');
            if (plus > 0) v = v[..plus];
            return v;
        }

        var version = asm.GetName().Version;
        return version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "0.0.0";
    }
}
