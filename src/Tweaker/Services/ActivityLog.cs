using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Tweaker.Services;

public sealed class ActivityLog
{
    public const int Keep = 2000;

    private readonly string? _path;
    private readonly Func<DateTime> _now;
    private readonly object _gate = new();
    private int _fileLines;

    public ActivityLog(string? path, Func<DateTime>? now = null)
    {
        _path = path;
        _now = now ?? (() => DateTime.Now);
        Lines = new ObservableCollection<string>(LoadTail());
    }

    public ObservableCollection<string> Lines { get; }

    public void Add(string source, string message)
    {
        string line = _now().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + "  [" + source + "] " + message;
        Append(line);
        System.Windows.Threading.Dispatcher? dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess()) AddLine(line);
        else dispatcher.BeginInvoke(() => AddLine(line));
    }

    public Action<string> For(string source) => message => Add(source, message);

    public void Watch(INotifyPropertyChanged vm, string source)
    {
        System.Reflection.PropertyInfo? property = vm.GetType().GetProperty("Message");
        if (property is null || property.PropertyType != typeof(string)) return;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != "Message" || property.GetValue(vm) is not string message) return;
            string text = message.Trim();
            if (text.Length == 0 || text.EndsWith("…", StringComparison.Ordinal)) return;
            if (text.EndsWith("?", StringComparison.Ordinal) || text.Contains("? Click", StringComparison.Ordinal)) return;
            Add(source, text);
        };
    }

    private void AddLine(string line)
    {
        Lines.Add(line);
        while (Lines.Count > Keep) Lines.RemoveAt(0);
    }

    private IEnumerable<string> LoadTail()
    {
        if (_path is null) return Array.Empty<string>();
        try
        {
            if (!File.Exists(_path)) return Array.Empty<string>();
            string[] all = File.ReadAllLines(_path);
            _fileLines = all.Length;
            return all.Skip(Math.Max(0, all.Length - Keep)).ToList();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private void Append(string line)
    {
        if (_path is null) return;
        lock (_gate)
        {
            try
            {
                string? dir = Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.AppendAllLines(_path, new[] { line });
                if (++_fileLines > Keep * 4) Trim();
            }
            catch
            {
            }
        }
    }

    private void Trim()
    {
        string[] all = File.ReadAllLines(_path!);
        string temp = _path + ".tmp";
        File.WriteAllLines(temp, all.Skip(Math.Max(0, all.Length - Keep)));
        File.Move(temp, _path!, overwrite: true);
        _fileLines = Math.Min(all.Length, Keep);
    }
}
