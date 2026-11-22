using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Tweaker.Services;

public sealed class JsonConfigDocument : IConfigDocument
{
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly JsonNode _root;
    private readonly List<Slot> _slots = new();
    private readonly List<ConfigEntry> _entries = new();

    private sealed record Slot(JsonNode Parent, string? Name, int Index, JsonValueKind Kind);

    private JsonConfigDocument(JsonNode root)
    {
        _root = root;
        Walk(root, "", "", top: true);
    }

    public ConfigFormat Format => ConfigFormat.Json;
    public IReadOnlyList<ConfigEntry> Entries => _entries;

    public static JsonConfigDocument? Parse(string text)
    {
        try
        {
            JsonNode? root = JsonNode.Parse(text, documentOptions: new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            });
            return root is JsonObject or JsonArray ? new JsonConfigDocument(root) : null;
        }
        catch
        {
            return null;
        }
    }

    public void Set(int index, string value)
    {
        Slot slot = _slots[index];
        JsonNode? node = Create(slot.Kind, value);
        if (slot.Parent is JsonObject obj) obj[slot.Name!] = node;
        else if (slot.Parent is JsonArray arr) arr[slot.Index] = node;
        _entries[index] = _entries[index] with { Value = value };
    }

    public string Serialize() => _root.ToJsonString(WriteOptions);

    private void Walk(JsonNode node, string section, string prefix, bool top)
    {
        if (node is JsonObject obj)
        {
            foreach (KeyValuePair<string, JsonNode?> property in obj.ToList())
            {
                string key = prefix.Length == 0 ? property.Key : prefix + "." + property.Key;
                if (property.Value is JsonObject or JsonArray)
                {
                    if (top) Walk(property.Value, property.Key, "", top: false);
                    else Walk(property.Value, section, key, top: false);
                }
                else
                {
                    Leaf(obj, property.Key, -1, property.Value, section, key);
                }
            }
        }
        else if (node is JsonArray arr)
        {
            for (int i = 0; i < arr.Count; i++)
            {
                string key = prefix + "[" + i.ToString(CultureInfo.InvariantCulture) + "]";
                JsonNode? child = arr[i];
                if (child is JsonObject or JsonArray) Walk(child, section, key, top: false);
                else Leaf(arr, null, i, child, section, key);
            }
        }
    }

    private void Leaf(JsonNode parent, string? name, int index, JsonNode? value, string section, string key)
    {
        JsonValueKind kind = value?.GetValueKind() ?? JsonValueKind.Null;
        string text = kind switch
        {
            JsonValueKind.String => value!.GetValue<string>(),
            JsonValueKind.Null => "null",
            _ => value!.ToJsonString(),
        };
        _slots.Add(new Slot(parent, name, index, kind));
        _entries.Add(new ConfigEntry(section, key, text));
    }

    private static JsonNode? Create(JsonValueKind kind, string text)
    {
        string t = text.Trim();
        switch (kind)
        {
            case JsonValueKind.True or JsonValueKind.False when bool.TryParse(t, out bool b):
                return JsonValue.Create(b);
            case JsonValueKind.Number when long.TryParse(t, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long l):
                return JsonValue.Create(l);
            case JsonValueKind.Number when double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out double d) && double.IsFinite(d):
                return JsonValue.Create(d);
            case JsonValueKind.Null when t == "null":
                return null;
        }
        return JsonValue.Create(text);
    }
}
