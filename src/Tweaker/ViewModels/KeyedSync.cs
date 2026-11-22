using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Tweaker.ViewModels;

public static class KeyedSync
{
    public static void Apply<TItem, TSource>(ObservableCollection<TItem> target, IReadOnlyList<TSource> next,
        Func<TSource, string> sourceKey, Func<TItem, string> itemKey,
        Func<TSource, TItem> create, Action<TItem, TSource> update)
    {
        var wanted = new HashSet<string>(next.Select(sourceKey), StringComparer.Ordinal);
        for (int i = target.Count - 1; i >= 0; i--)
        {
            if (!wanted.Contains(itemKey(target[i]))) target.RemoveAt(i);
        }

        for (int i = 0; i < next.Count; i++)
        {
            string key = sourceKey(next[i]);
            if (i < target.Count && itemKey(target[i]) == key)
            {
                update(target[i], next[i]);
                continue;
            }

            int found = -1;
            for (int j = i + 1; j < target.Count; j++)
            {
                if (itemKey(target[j]) == key) { found = j; break; }
            }

            if (found >= 0)
            {
                target.Move(found, i);
                update(target[i], next[i]);
            }
            else
            {
                target.Insert(i, create(next[i]));
            }
        }

        while (target.Count > next.Count) target.RemoveAt(target.Count - 1);
    }
}
