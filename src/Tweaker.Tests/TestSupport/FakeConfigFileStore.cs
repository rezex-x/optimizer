using System;
using System.Collections.Generic;
using System.Text;
using Tweaker.Services;

namespace Tweaker.Tests.TestSupport;

public sealed class FakeConfigFileStore : IConfigFileStore
{
    public Dictionary<string, ConfigFile> Files { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<(string Path, string Text, Encoding Encoding)> Saved { get; } = new();
    public string? SaveResult { get; set; }

    public Action? DuringSave { get; set; }

    public ConfigReadResult Read(string path) =>
        Files.TryGetValue(path, out ConfigFile? file) ? new ConfigReadResult(file, null) : new ConfigReadResult(null, "file not found");

    public string? Save(string path, string text, Encoding encoding)
    {
        Saved.Add((path, text, encoding));
        DuringSave?.Invoke();
        if (SaveResult is null && Files.TryGetValue(path, out ConfigFile? file)) Files[path] = file with { Text = text };
        return SaveResult;
    }
}
