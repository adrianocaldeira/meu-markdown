using Microsoft.Win32;

namespace MeuMarkdown.Services;

public interface IRegistryReader
{
    string? ReadString(RegistryHive hive, string subKey, string valueName);
}
