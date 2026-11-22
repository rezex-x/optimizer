using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Tweaker.Services;

namespace Tweaker.Views;

public partial class ConfirmDialog : Window
{
    public ConfirmDialog(TweakConfirmRequest request)
    {
        InitializeComponent();
        DataContext = ConfirmDialogModel.Single(request);
    }

    public ConfirmDialog(string title, IReadOnlyList<TweakConfirmRequest> items)
    {
        InitializeComponent();
        DataContext = ConfirmDialogModel.Batch(title, items);
    }

    private void Apply_Click(object sender, RoutedEventArgs e) { DialogResult = true; Close(); }
    private void Cancel_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }
}

internal sealed class ConfirmDialogModel
{
    private static readonly Brush HighFg = new SolidColorBrush(Color.FromRgb(0xD0, 0x80, 0x80));
    private static readonly Brush NormalFg = new SolidColorBrush(Color.FromRgb(0x8A, 0x8A, 0x8A));

    public string Heading { get; init; } = "";
    public string ApplyLabel { get; init; } = "Apply";
    public bool IsSingle { get; init; }
    public bool IsBatch => !IsSingle;

    public string RiskText { get; init; } = "";
    public string Category { get; init; } = "";
    public bool ShowRestart { get; init; }
    public string Description { get; init; } = "";
    public string Benefit { get; init; } = "";
    public string Downsides { get; init; } = "";
    public string? Warning { get; init; }
    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);
    public bool HasBenefit => !string.IsNullOrWhiteSpace(Benefit);
    public bool HasDownsides => !string.IsNullOrWhiteSpace(Downsides);
    public bool HasWarning => !string.IsNullOrWhiteSpace(Warning);

    public IReadOnlyList<Row> Rows { get; init; } = new List<Row>();
    public string RestartSummary { get; init; } = "";
    public bool HasRestartSummary => !string.IsNullOrWhiteSpace(RestartSummary);

    public static ConfirmDialogModel Single(TweakConfirmRequest r) => new()
    {
        IsSingle = true,
        Heading = r.Name,
        ApplyLabel = "Apply",
        RiskText = r.Risk.ToString().ToUpperInvariant(),
        Category = r.Category,
        ShowRestart = r.RequiresRestart,
        Description = r.Description,
        Benefit = r.Benefit,
        Downsides = r.Downsides,
        Warning = r.Warning,
    };

    public static ConfirmDialogModel Batch(string title, IReadOnlyList<TweakConfirmRequest> items)
    {
        int restarts = items.Count(i => i.RequiresRestart);
        string? warning = items.FirstOrDefault(i => i.Warning is not null)?.Warning;
        return new ConfirmDialogModel
        {
            IsSingle = false,
            Heading = title,
            ApplyLabel = "Apply All",
            Warning = warning,
            Rows = items.Select(i => new Row
            {
                Name = i.Name,
                RiskText = i.Risk.ToString().ToUpperInvariant(),
                RiskFg = i.Risk == Tweaker.Core.Models.RiskLevel.High ? HighFg : NormalFg,
                ShowRestart = i.RequiresRestart,
            }).ToList(),
            RestartSummary = restarts > 0 ? $"{restarts} of {items.Count} require a restart." : "",
        };
    }

    public sealed class Row
    {
        public string Name { get; init; } = "";
        public string RiskText { get; init; } = "";
        public Brush RiskFg { get; init; } = NormalFg;
        public bool ShowRestart { get; init; }
    }
}
