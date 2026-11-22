using System.Collections.ObjectModel;
using System.Linq;
using Tweaker.ViewModels;
using Xunit;

namespace Tweaker.Tests.ViewModels;

public class KeyedSyncTests
{
    private sealed class Row
    {
        public Row(string key, int value) { Key = key; Value = value; }
        public string Key { get; }
        public int Value { get; set; }
    }

    private static void Sync(ObservableCollection<Row> rows, params (string Key, int Value)[] next) =>
        KeyedSync.Apply(rows, next, n => n.Key, r => r.Key, n => new Row(n.Key, n.Value), (r, n) => r.Value = n.Value);

    [Fact]
    public void Fills_an_empty_collection_in_order()
    {
        var rows = new ObservableCollection<Row>();
        Sync(rows, ("a", 1), ("b", 2));
        Assert.Equal(new[] { "a", "b" }, rows.Select(r => r.Key));
    }

    [Fact]
    public void Surviving_keys_keep_their_row_object_and_are_updated()
    {
        var rows = new ObservableCollection<Row>();
        Sync(rows, ("a", 1), ("b", 2));
        Row a = rows[0];

        Sync(rows, ("a", 10), ("b", 20));

        Assert.Same(a, rows[0]);
        Assert.Equal(10, rows[0].Value);
        Assert.Equal(20, rows[1].Value);
    }

    [Fact]
    public void Missing_keys_are_removed_and_new_ones_inserted_in_place()
    {
        var rows = new ObservableCollection<Row>();
        Sync(rows, ("a", 1), ("b", 2), ("c", 3));
        Sync(rows, ("a", 1), ("x", 9), ("c", 3));
        Assert.Equal(new[] { "a", "x", "c" }, rows.Select(r => r.Key));
    }

    [Fact]
    public void Reordering_moves_the_existing_rows()
    {
        var rows = new ObservableCollection<Row>();
        Sync(rows, ("a", 1), ("b", 2), ("c", 3));
        Row c = rows[2];

        Sync(rows, ("c", 3), ("a", 1), ("b", 2));

        Assert.Equal(new[] { "c", "a", "b" }, rows.Select(r => r.Key));
        Assert.Same(c, rows[0]);
    }

    [Fact]
    public void An_empty_list_clears_the_collection()
    {
        var rows = new ObservableCollection<Row>();
        Sync(rows, ("a", 1));
        Sync(rows);
        Assert.Empty(rows);
    }
}
