using System;
using System.IO;
using System.Text;

namespace Tweaker.Services;

public sealed record ConfigFile(string Text, Encoding Encoding, bool IsReadOnly);

public sealed record ConfigReadResult(ConfigFile? File, string? Error);

public interface IConfigFileStore
{
    ConfigReadResult Read(string path);

    string? Save(string path, string text, Encoding encoding);
}

public sealed class ConfigFileStore : IConfigFileStore
{
    public const long MaxBytes = 5 * 1024 * 1024;

    public ConfigReadResult Read(string path)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists) return new ConfigReadResult(null, "file not found");
            if (info.Length > MaxBytes) return new ConfigReadResult(null, "file is too large (over 5 MB)");

            byte[] bytes = File.ReadAllBytes(path);
            if (!HasByteOrderMark(bytes))
            {
                try
                {
                    string utf8 = new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(bytes);
                    return new ConfigReadResult(new ConfigFile(utf8, new UTF8Encoding(false), info.IsReadOnly), null);
                }
                catch (DecoderFallbackException)
                {
                    Encoding latin1 = Encoding.GetEncoding("iso-8859-1", EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
                    return new ConfigReadResult(new ConfigFile(latin1.GetString(bytes), latin1, info.IsReadOnly), null);
                }
            }
            using var reader = new StreamReader(new MemoryStream(bytes), new UTF8Encoding(false), detectEncodingFromByteOrderMarks: true);
            string text = reader.ReadToEnd();
            return new ConfigReadResult(new ConfigFile(text, reader.CurrentEncoding, info.IsReadOnly), null);
        }
        catch (Exception ex)
        {
            return new ConfigReadResult(null, ex.Message);
        }
    }

    public string? Save(string path, string text, Encoding encoding)
    {
        string? dir = Path.GetDirectoryName(path);
        if (string.IsNullOrEmpty(dir)) dir = ".";
        string temp = Path.Combine(dir, Path.GetFileName(path) + "." + Path.GetRandomFileName() + ".tmp");
        string bak = path + ".bak";
        try
        {
            if (!File.Exists(path)) return "the file no longer exists";
            if (new FileInfo(path).IsReadOnly) return "the file is read-only — clear Read-only in its properties to save";
            File.WriteAllText(temp, text, encoding);
            File.Replace(temp, path, bak, ignoreMetadataErrors: true);
            return null;
        }
        catch (EncoderFallbackException)
        {
            try { File.Delete(temp); } catch { }
            return "a value has characters this file's encoding (ANSI) can't store";
        }
        catch (Exception ex)
        {
            if (!File.Exists(path) && File.Exists(bak))
            {
                try { File.Move(bak, path); } catch { }
            }
            try { File.Delete(temp); } catch { }
            return File.Exists(path) ? ex.Message : ex.Message + " — the original is saved as " + Path.GetFileName(bak);
        }
    }

    private static bool HasByteOrderMark(byte[] b) =>
        (b.Length >= 3 && b[0] == 0xEF && b[1] == 0xBB && b[2] == 0xBF)
        || (b.Length >= 2 && ((b[0] == 0xFF && b[1] == 0xFE) || (b[0] == 0xFE && b[1] == 0xFF)));
}
