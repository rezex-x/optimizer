using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Tweaker.Core.Catalog;
using Tweaker.Core.Models;
using Tweaker.Core.Services;
using Tweaker.Tests.TestSupport;
using Tweaker.ViewModels;
using Xunit;

namespace Tweaker.Tests.ViewModels;

public class ConfigViewModelTests
{
    private static Tweak[] CategoryTweaks() => new[]
    {
        new Tweak { Id = "t1", Name = "Hide Search", Category = "Taskbar" },
        new Tweak { Id = "e1", Name = "Show Extensions", Category = "Explorer" },
        new Tweak { Id = "t2", Name = "Small Icons", Category = "Taskbar" },
        new Tweak { Id = "v1", Name = "Turn Off Peek", Category = "Visual Effects" },
    };

    [Fact]
    public void Categories_are_All_then_each_category_in_catalog_order()
    {
        var vm = new ConfigViewModel(CategoryTweaks(), new FakeTweakEngine(), new FakeConfirmationService());

        Assert.Equal(new[] { "All", "Taskbar", "Explorer", "Visual Effects" }, vm.Categories);
        Assert.Equal("All", vm.SelectedCategory);
        Assert.Equal(new[] { "Taskbar", "Explorer", "Visual Effects" }, vm.Groups.Select(g => g.Name));
    }

    [Fact]
    public void Selecting_a_category_shows_only_that_group_and_keeps_the_rows()
    {
        var vm = new ConfigViewModel(CategoryTweaks(), new FakeTweakEngine(), new FakeConfirmationService());
        TweakViewModel before = vm.Groups.Single(g => g.Name == "Taskbar").Tweaks[0];

        vm.SelectedCategory = "Taskbar";

        CategoryGroup group = Assert.Single(vm.Groups);
        Assert.Equal("Taskbar", group.Name);
        Assert.Equal(new[] { "t1", "t2" }, group.Tweaks.Select(t => t.Id));
        Assert.Same(before, group.Tweaks[0]);

        vm.SelectedCategory = "All";
        Assert.Equal(3, vm.Groups.Count);
    }

    [Fact]
    public void Category_and_search_combine()
    {
        var vm = new ConfigViewModel(CategoryTweaks(), new FakeTweakEngine(), new FakeConfirmationService());

        vm.SelectedCategory = "Taskbar";
        vm.SearchText = "icons";

        CategoryGroup group = Assert.Single(vm.Groups);
        Assert.Equal(new[] { "t2" }, group.Tweaks.Select(t => t.Id));

        vm.SearchText = "extensions";
        Assert.Empty(vm.Groups);
    }

    [Fact]
    public void Groups_only_config_surface_categories()
    {
        var tweaks = new[]
        {
            new Tweak { Id = "a", Name = "A", Category = "Taskbar" },
            new Tweak { Id = "b", Name = "B", Category = "Explorer" },
        };
        var vm = new ConfigViewModel(tweaks, new FakeTweakEngine(), new FakeConfirmationService());

        Assert.All(vm.Groups, g => Assert.True(ConfigSurface.IsConfig(g.Name)));
        Assert.Equal(new[] { "Explorer", "Taskbar" }, vm.Groups.Select(g => g.Name).OrderBy(n => n));
    }

    [Fact]
    public void Columns_partition_every_group_once_and_balance_by_height()
    {
        var tweaks =
            Enumerable.Range(0, 8).Select(i => new Tweak { Id = $"t{i}", Name = $"T{i}", Category = "Taskbar" })
            .Concat(Enumerable.Range(0, 5).Select(i => new Tweak { Id = $"s{i}", Name = $"S{i}", Category = "Start Menu" }))
            .Concat(Enumerable.Range(0, 3).Select(i => new Tweak { Id = $"e{i}", Name = $"E{i}", Category = "Explorer" }))
            .Concat(Enumerable.Range(0, 2).Select(i => new Tweak { Id = $"n{i}", Name = $"N{i}", Category = "Notifications" }))
            .ToArray();
        var vm = new ConfigViewModel(tweaks, new FakeTweakEngine(), new FakeConfirmationService());

        var inColumns = vm.LeftColumn.Concat(vm.RightColumn).ToList();
        Assert.Equal(vm.Groups.Count, inColumns.Count);
        Assert.Equal(vm.Groups.OrderBy(g => g.Name).Select(g => g.Name),
                     inColumns.OrderBy(g => g.Name).Select(g => g.Name));

        int left = vm.LeftColumn.Sum(g => g.Tweaks.Count);
        int right = vm.RightColumn.Sum(g => g.Tweaks.Count);
        Assert.True(System.Math.Abs(left - right) <= 8,
            $"columns unbalanced: left={left}, right={right}");
    }

    [Fact]
    public void SearchText_filters_rows_by_name_or_description_across_both_columns()
    {
        var tweaks = new[]
        {
            new Tweak { Id = "a", Name = "Hide Widgets", Category = "Taskbar", Description = "weather" },
            new Tweak { Id = "b", Name = "Show Extensions", Category = "Explorer", Description = "file types" },
            new Tweak { Id = "c", Name = "Hide Task View", Category = "Taskbar", Description = "" },
        };
        var vm = new ConfigViewModel(tweaks, new FakeTweakEngine(), new FakeConfirmationService());

        vm.SearchText = "hide";
        var shown = vm.LeftColumn.Concat(vm.RightColumn).SelectMany(g => g.Tweaks).Select(t => t.Id).OrderBy(i => i);
        Assert.Equal(new[] { "a", "c" }, shown);

        vm.SearchText = "file types";
        Assert.Equal("b", vm.LeftColumn.Concat(vm.RightColumn).SelectMany(g => g.Tweaks).Single().Id);

        vm.SearchText = "";
        Assert.Equal(3, vm.Groups.Sum(g => g.Tweaks.Count));
    }

    [Fact]
    public async Task AppliedCount_counts_applied_rows_and_updates_when_a_row_is_toggled()
    {
        var engine = new FakeTweakEngine();
        engine.Overrides["a"] = TweakState.Applied;
        var tweaks = new[]
        {
            new Tweak { Id = "a", Name = "A", Category = "Taskbar", Type = TweakType.Registry },
            new Tweak { Id = "b", Name = "B", Category = "Explorer", Type = TweakType.Registry },
        };
        var vm = new ConfigViewModel(tweaks, engine, new FakeConfirmationService { Result = true });
        Assert.Equal(1, vm.AppliedCount);

        var raised = new List<string>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName!);
        TweakViewModel b = vm.Groups.SelectMany(g => g.Tweaks).Single(t => t.Id == "b");

        await b.ToggleAsync();

        Assert.Equal(2, vm.AppliedCount);
        Assert.Contains(nameof(vm.AppliedCount), raised);
    }

    [Fact]
    public void RefreshStates_picks_up_a_change_made_elsewhere_and_updates_the_count()
    {
        var engine = new FakeTweakEngine();
        var tweaks = new[] { new Tweak { Id = "a", Name = "A", Category = "Taskbar", Type = TweakType.Registry } };
        var vm = new ConfigViewModel(tweaks, engine, new FakeConfirmationService { Result = true });
        Assert.Equal(0, vm.AppliedCount);
        var raised = new List<string>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName!);

        engine.Overrides["a"] = TweakState.Applied;
        vm.RefreshStates();

        Assert.True(vm.Groups.SelectMany(g => g.Tweaks).Single().IsApplied);
        Assert.Equal(1, vm.AppliedCount);
        Assert.Contains(nameof(vm.AppliedCount), raised);
    }

    [Fact]
    public void AppliedCount_counts_every_config_tweak_not_just_the_search_results()
    {
        var engine = new FakeTweakEngine();
        engine.Overrides["a"] = TweakState.Applied;
        var tweaks = new[]
        {
            new Tweak { Id = "a", Name = "Alpha", Category = "Taskbar", Type = TweakType.Registry },
            new Tweak { Id = "b", Name = "Bravo", Category = "Explorer", Type = TweakType.Registry },
        };
        var vm = new ConfigViewModel(tweaks, engine, new FakeConfirmationService { Result = true });

        vm.SearchText = "Bravo";

        Assert.Equal(1, vm.AppliedCount);
    }
}
