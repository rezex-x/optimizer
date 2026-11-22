using System;
using System.Collections.Generic;
using System.ComponentModel;
using Tweaker.ViewModels;

namespace Tweaker.Tests.TestSupport;

public sealed class FakeProfileState : IProfileState
{
    private readonly HashSet<string> _managed = new(StringComparer.OrdinalIgnoreCase);

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool IsActive { get; private set; }
    public string BadgeText { get; private set; } = "";

    public bool Manages(string serviceName, string displayName) =>
        IsActive && (_managed.Contains(serviceName) || _managed.Contains(displayName));

    public void Set(bool active, string badge = "", params string[] managed)
    {
        IsActive = active;
        BadgeText = badge;
        _managed.Clear();
        foreach (string m in managed) _managed.Add(m);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsActive)));
    }
}
