using System.Collections.ObjectModel;

namespace Tweaker.ViewModels;

public sealed class CategoryGroup
{
    public CategoryGroup(string name, IEnumerable<TweakViewModel> tweaks)
    {
        Name = name;
        Tweaks = new ObservableCollection<TweakViewModel>(tweaks);
    }

    public string Name { get; }
    public ObservableCollection<TweakViewModel> Tweaks { get; }
}
