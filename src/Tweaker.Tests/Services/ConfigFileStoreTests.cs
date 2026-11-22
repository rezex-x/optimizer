using System.IO;
using System.Text;
using Tweaker.Services;
using Tweaker.Tests.TestSupport;
using Xunit;

namespace Tweaker.Tests.Services;

public class ConfigFileStoreTests
{
    private readonly ConfigFileStore _store = new();

    [Fact]
    public void Save_keeps_a_bak_of_the_previous_file()
    {
        using var dir = new TempDir();
        string path = Path.Combine(dir.Path, "Game.ini");
        File.WriteAllText(path, "a=1\n", new UTF8Encoding(false));

        ConfigReadResult read = _store.Read(path);
        Assert.Null(read.Error);
        Assert.Equal("a=1\n", read.File!.Text);
        Assert.False(read.File.IsReadOnly);

        Assert.Null(_store.Save(path, "a=2\n", read.File.Encoding));

        Assert.Equal("a=1\n", File.ReadAllText(path + ".bak"));
        Assert.Equal("a=2\n", File.ReadAllText(path));
        Assert.Empty(Directory.GetFiles(dir.Path, "*.tmp"));
    }

    [Fact]
    public void An_ansi_file_refuses_characters_it_cannot_store_and_stays_unchanged()
    {
        using var dir = new TempDir();
        string path = Path.Combine(dir.Path, "ansi.ini");
        byte[] original = { (byte)'n', (byte)'=', (byte)'C', (byte)'a', (byte)'f', 0xE9, (byte)'\n' };
        File.WriteAllBytes(path, original);

        ConfigFile file = _store.Read(path).File!;
        Assert.Equal("n=Caf\u00E9\n", file.Text);

        Assert.Equal("a value has characters this file's encoding (ANSI) can't store", _store.Save(path, "n=\u0141\n", file.Encoding));
        Assert.Equal(original, File.ReadAllBytes(path));
        Assert.Empty(Directory.GetFiles(dir.Path, "*.tmp"));
        Assert.False(File.Exists(path + ".bak"));

        Assert.Null(_store.Save(path, "n=Caf\u00E8\n", file.Encoding));
        Assert.Equal(new byte[] { (byte)'n', (byte)'=', (byte)'C', (byte)'a', (byte)'f', 0xE8, (byte)'\n' }, File.ReadAllBytes(path));
    }

    [Fact]
    public void A_file_deleted_since_it_was_opened_is_not_recreated()
    {
        using var dir = new TempDir();
        string path = Path.Combine(dir.Path, "gone.ini");

        Assert.Equal("the file no longer exists", _store.Save(path, "a=1\n", new UTF8Encoding(false)));
        Assert.False(File.Exists(path));
        Assert.Empty(Directory.GetFiles(dir.Path));
    }

    [Fact]
    public void The_encoding_and_byte_order_mark_survive_a_save()
    {
        using var dir = new TempDir();
        string utf16 = Path.Combine(dir.Path, "u16.ini");
        File.WriteAllText(utf16, "k=v\r\n", Encoding.Unicode);
        string plain = Path.Combine(dir.Path, "u8.ini");
        File.WriteAllText(plain, "k=v\n", new UTF8Encoding(false));

        ConfigFile a = _store.Read(utf16).File!;
        ConfigFile b = _store.Read(plain).File!;
        Assert.Equal("k=v\r\n", a.Text);
        _store.Save(utf16, "k=w\r\n", a.Encoding);
        _store.Save(plain, "k=w\n", b.Encoding);

        byte[] u16 = File.ReadAllBytes(utf16);
        Assert.Equal(new byte[] { 0xFF, 0xFE }, new[] { u16[0], u16[1] });
        Assert.Equal("k=w\r\n", Encoding.Unicode.GetString(u16, 2, u16.Length - 2));
        byte[] u8 = File.ReadAllBytes(plain);
        Assert.Equal((byte)'k', u8[0]);
    }

    [Fact]
    public void A_read_only_file_is_never_written()
    {
        using var dir = new TempDir();
        string path = Path.Combine(dir.Path, "locked.ini");
        File.WriteAllText(path, "a=1\n");
        File.SetAttributes(path, FileAttributes.ReadOnly);
        try
        {
            ConfigFile file = _store.Read(path).File!;
            Assert.True(file.IsReadOnly);

            string? error = _store.Save(path, "a=2\n", file.Encoding);

            Assert.Contains("read-only", error);
            Assert.Equal("a=1\n", File.ReadAllText(path));
            Assert.False(File.Exists(path + ".bak"));
        }
        finally
        {
            File.SetAttributes(path, FileAttributes.Normal);
        }
    }

    [Fact]
    public void A_missing_file_says_so()
    {
        using var dir = new TempDir();
        ConfigReadResult read = _store.Read(Path.Combine(dir.Path, "nope.ini"));
        Assert.Null(read.File);
        Assert.Equal("file not found", read.Error);
    }

    [Fact]
    public void An_ansi_file_keeps_every_byte_it_did_not_edit()
    {
        using var dir = new TempDir();
        string path = Path.Combine(dir.Path, "ansi.ini");
        File.WriteAllBytes(path, new byte[] { (byte)'n', (byte)'=', (byte)'C', (byte)'a', (byte)'f', 0xE9, (byte)'\n', (byte)'k', (byte)'=', (byte)'1', (byte)'\n' });

        ConfigFile file = _store.Read(path).File!;
        Assert.Equal("n=Café\nk=1\n", file.Text);
        Assert.Null(_store.Save(path, file.Text.Replace("k=1", "k=2"), file.Encoding));

        Assert.Equal(new byte[] { (byte)'n', (byte)'=', (byte)'C', (byte)'a', (byte)'f', 0xE9, (byte)'\n', (byte)'k', (byte)'=', (byte)'2', (byte)'\n' },
            File.ReadAllBytes(path));
    }
}
