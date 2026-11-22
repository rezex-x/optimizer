using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Tweaker.Services;

public sealed record DetectedConfig(string Game, string FileName, string Path);

public static class ConfigDetector
{
    public static IReadOnlyList<DetectedConfig> Find(Func<string, string?> env, Func<string, IEnumerable<string>> subdirs,
        Func<string, bool> exists, string? steamPath)
    {
        var found = new List<DetectedConfig>();
        void Add(string game, string path)
        {
            try
            {
                if (exists(path)) found.Add(new DetectedConfig(game, System.IO.Path.GetFileName(path), path));
            }
            catch
            {
            }
        }

        string? local = env("LOCALAPPDATA");
        if (!string.IsNullOrEmpty(local))
        {
            Add("Fortnite", System.IO.Path.Combine(local, "FortniteGame", "Saved", "Config", "WindowsClient", "GameUserSettings.ini"));
            string valorant = System.IO.Path.Combine(local, "VALORANT", "Saved", "Config");
            Add("VALORANT", System.IO.Path.Combine(valorant, "WindowsClient", "GameUserSettings.ini"));
            foreach (string account in Children(subdirs, valorant))
            {
                Add("VALORANT", System.IO.Path.Combine(account, "WindowsClient", "GameUserSettings.ini"));
                Add("VALORANT", System.IO.Path.Combine(account, "Windows", "GameUserSettings.ini"));
            }
        }
        if (!string.IsNullOrEmpty(steamPath))
        {
            foreach (string user in Children(subdirs, System.IO.Path.Combine(steamPath, "userdata")))
                Add("Counter-Strike 2", System.IO.Path.Combine(user, "730", "local", "cfg", "cs2_video.txt"));
        }
        string? profile = env("USERPROFILE");
        if (!string.IsNullOrEmpty(profile))
            Add("Apex Legends", System.IO.Path.Combine(profile, "Saved Games", "Respawn", "Apex", "local", "videoconfig.txt"));
        return found;
    }

    public static string Display(string path, Func<string, string?> env)
    {
        foreach (string name in new[] { "LOCALAPPDATA", "USERPROFILE" })
        {
            string? root = env(name)?.TrimEnd('\\');
            if (!string.IsNullOrEmpty(root) && path.StartsWith(root + "\\", StringComparison.OrdinalIgnoreCase))
                return "%" + name + "%" + path[root.Length..];
        }
        return path;
    }

    private static IEnumerable<string> Children(Func<string, IEnumerable<string>> subdirs, string dir)
    {
        try
        {
            return subdirs(dir).ToList();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }
}
