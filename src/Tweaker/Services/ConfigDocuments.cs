using System;
using System.Collections.Generic;
using System.Linq;

namespace Tweaker.Services;

public enum ConfigFormat { Ini, KeyValues, Cfg, Json }

public sealed record ConfigEntry(string Section, string Key, string Value);

public interface IConfigDocument
{
    ConfigFormat Format { get; }

    IReadOnlyList<ConfigEntry> Entries { get; }

    void Set(int index, string value);

    string Serialize();
}

public abstract class LineConfigDocument : IConfigDocument
{
    private readonly List<string> _lines = new();
    private readonly List<string> _endings = new();
    private readonly List<(int Line, int Start, int Length)> _spans = new();
    private readonly List<ConfigEntry> _entries = new();

    protected LineConfigDocument(string text)
    {
        int start = 0;
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] != '\n') continue;
            bool crlf = i > start && text[i - 1] == '\r';
            _lines.Add(text[start..(crlf ? i - 1 : i)]);
            _endings.Add(crlf ? "\r\n" : "\n");
            start = i + 1;
        }
        _lines.Add(text[start..]);
        _endings.Add("");
    }

    public abstract ConfigFormat Format { get; }
    public IReadOnlyList<ConfigEntry> Entries => _entries;
    protected IReadOnlyList<string> Lines => _lines;

    protected void Add(string section, string key, int line, int start, int length)
    {
        _spans.Add((line, start, length));
        _entries.Add(new ConfigEntry(section, key, _lines[line].Substring(start, length)));
    }

    public void Set(int index, string value)
    {
        value = (value ?? "").Replace("\r", "").Replace("\n", "");
        (int line, int start, int length) = _spans[index];
        string text = _lines[line];
        _lines[line] = text[..start] + value + text[(start + length)..];
        int delta = value.Length - length;
        for (int i = 0; i < _spans.Count; i++)
        {
            if (i != index && _spans[i].Line == line && _spans[i].Start > start)
                _spans[i] = (line, _spans[i].Start + delta, _spans[i].Length);
        }
        _spans[index] = (line, start, value.Length);
        _entries[index] = _entries[index] with { Value = value };
    }

    public string Serialize() => string.Concat(_lines.Select((line, i) => line + _endings[i]));
}

public sealed class IniDocument : LineConfigDocument
{
    private IniDocument(string text) : base(text)
    {
        string section = "";
        for (int i = 0; i < Lines.Count; i++)
        {
            string line = Lines[i];
            string t = line.Trim().TrimStart('﻿').Trim();
            if (t.Length == 0 || t[0] == ';' || t[0] == '#') continue;
            if (t.Length >= 2 && t[0] == '[' && t[^1] == ']')
            {
                section = t[1..^1].Trim();
                continue;
            }
            int eq = line.IndexOf('=');
            if (eq <= 0) continue;
            string key = line[..eq].Trim().TrimStart('﻿');
            if (key.Length == 0) continue;

            int start = eq + 1, end = line.Length;
            while (start < end && char.IsWhiteSpace(line[start])) start++;
            while (end > start && char.IsWhiteSpace(line[end - 1])) end--;
            if (end - start >= 2 && line[start] == '"' && line[end - 1] == '"')
            {
                start++;
                end--;
            }
            Add(section, key, i, start, end - start);
        }
    }

    public override ConfigFormat Format => ConfigFormat.Ini;
    public static IniDocument Parse(string text) => new(text);
}

public sealed class KeyValuesDocument : LineConfigDocument
{
    private KeyValuesDocument(string text) : base(text)
    {
        var blocks = new List<string>();
        string? pending = null;
        for (int i = 0; i < Lines.Count; i++)
        {
            string line = Lines[i];
            List<LineToken> tokens = LineTokens.Read(line);
            for (int k = 0; k < tokens.Count; k++)
            {
                LineToken token = tokens[k];
                if (token.IsBrace)
                {
                    if (line[token.Start] == '{') blocks.Add(pending ?? "");
                    else if (blocks.Count > 0) blocks.RemoveAt(blocks.Count - 1);
                    pending = null;
                    continue;
                }
                string name = line.Substring(token.Start, token.Length);
                if (k + 1 < tokens.Count && !tokens[k + 1].IsBrace)
                {
                    LineToken value = tokens[k + 1];
                    Add(blocks.Count > 0 ? blocks[^1] : "", name, i, value.Start, value.Length);
                    pending = null;
                    k++;
                    continue;
                }
                pending = name;
            }
        }
    }

    public override ConfigFormat Format => ConfigFormat.KeyValues;
    public static KeyValuesDocument Parse(string text) => new(text);
}

public sealed class CfgDocument : LineConfigDocument
{
    private static readonly HashSet<string> Commands = new(StringComparer.OrdinalIgnoreCase)
    {
        "bind", "unbind", "alias", "exec", "echo", "toggle", "incrementvar", "say", "connect",
    };

    private CfgDocument(string text) : base(text)
    {
        for (int i = 0; i < Lines.Count; i++)
        {
            string line = Lines[i];
            List<LineToken> tokens = LineTokens.Read(line);
            if (tokens.Count != 2 || tokens[0].IsBrace || tokens[1].IsBrace) continue;
            string name = line.Substring(tokens[0].Start, tokens[0].Length);
            if (Commands.Contains(name)) continue;
            Add("", name, i, tokens[1].Start, tokens[1].Length);
        }
    }

    public override ConfigFormat Format => ConfigFormat.Cfg;
    public static CfgDocument Parse(string text) => new(text);
}

public readonly record struct LineToken(int Start, int Length, bool Quoted, bool IsBrace);

public static class LineTokens
{
    public static List<LineToken> Read(string line)
    {
        var tokens = new List<LineToken>();
        int i = 0;
        while (i < line.Length)
        {
            char c = line[i];
            if (char.IsWhiteSpace(c) || c == ';')
            {
                i++;
                continue;
            }
            if (c == '/' && i + 1 < line.Length && line[i + 1] == '/') break;
            if (c == '{' || c == '}')
            {
                tokens.Add(new LineToken(i, 1, false, true));
                i++;
                continue;
            }
            if (c == '"')
            {
                int close = line.IndexOf('"', i + 1);
                if (close < 0) close = line.Length;
                tokens.Add(new LineToken(i + 1, close - i - 1, true, false));
                i = close + 1;
                continue;
            }
            int start = i;
            while (i < line.Length && !char.IsWhiteSpace(line[i]) && line[i] != '"' && line[i] != '{' && line[i] != '}' && line[i] != ';'
                   && !(line[i] == '/' && i + 1 < line.Length && line[i + 1] == '/'))
                i++;
            tokens.Add(new LineToken(start, i - start, false, false));
        }
        return tokens;
    }
}
