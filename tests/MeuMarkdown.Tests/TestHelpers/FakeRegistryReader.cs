using Microsoft.Win32;
using MeuMarkdown.Services;

namespace MeuMarkdown.Tests.TestHelpers;

public sealed class FakeRegistryReader : IRegistryReader
{
    private readonly Dictionary<string, string> _values = new();

    public FakeRegistryReader Set(RegistryHive hive, string subKey, string valueName, string value)
    {
        _values[Key(hive, subKey, valueName)] = value;
        return this;
    }

    public string? ReadString(RegistryHive hive, string subKey, string valueName)
        => _values.TryGetValue(Key(hive, subKey, valueName), out var v) ? v : null;

    private static string Key(RegistryHive hive, string subKey, string valueName)
        => $"{hive}|{subKey}|{valueName}";
}
