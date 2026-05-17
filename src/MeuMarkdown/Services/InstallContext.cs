using System.IO;
using Microsoft.Win32;

namespace MeuMarkdown.Services;

public enum InstallType { PerUser, PerMachine, Portable }

public sealed record InstallContext(InstallType Type, string InstallPath)
{
    private const string SubKey = @"Software\MeuMarkdown";

    public bool RequiresElevation => Type == InstallType.PerMachine;
    public bool SupportsAutoUpdate => Type != InstallType.Portable;

    public static InstallContext Detect()
        => Detect(new RegistryReader(), AppContext.BaseDirectory);

    public static InstallContext Detect(IRegistryReader registry, string basePath)
    {
        // 1) InstallScope explícito (v1.3.0+)
        var machineScope = registry.ReadString(RegistryHive.LocalMachine, SubKey, "InstallScope");
        if (string.Equals(machineScope, "machine", StringComparison.OrdinalIgnoreCase))
            return new InstallContext(InstallType.PerMachine, basePath);

        var userScope = registry.ReadString(RegistryHive.CurrentUser, SubKey, "InstallScope");
        if (string.Equals(userScope, "user", StringComparison.OrdinalIgnoreCase))
            return new InstallContext(InstallType.PerUser, basePath);

        // 2) Fallback v1.2.x: comparar path com InstallPath gravado
        var normBase = NormalizePath(basePath);

        var machinePath = registry.ReadString(RegistryHive.LocalMachine, SubKey, "InstallPath");
        if (!string.IsNullOrEmpty(machinePath) && PathMatches(machinePath, normBase))
            return new InstallContext(InstallType.PerMachine, basePath);

        var userPath = registry.ReadString(RegistryHive.CurrentUser, SubKey, "InstallPath");
        if (!string.IsNullOrEmpty(userPath) && PathMatches(userPath, normBase))
            return new InstallContext(InstallType.PerUser, basePath);

        // 3) Heurística por path
        if (normBase.Contains(@"\program files", StringComparison.OrdinalIgnoreCase))
            return new InstallContext(InstallType.PerMachine, basePath);

        var localAppDataPrograms = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs");
        if (normBase.StartsWith(NormalizePath(localAppDataPrograms), StringComparison.OrdinalIgnoreCase))
            return new InstallContext(InstallType.PerUser, basePath);

        return new InstallContext(InstallType.Portable, basePath);
    }

    private static string NormalizePath(string path)
        => Path.GetFullPath(path).TrimEnd('\\', '/');

    private static bool PathMatches(string a, string b)
        => string.Equals(NormalizePath(a), NormalizePath(b), StringComparison.OrdinalIgnoreCase);
}
