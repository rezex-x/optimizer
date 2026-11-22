using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tweaker.Services;

public sealed class JsonProfileStore : IProfileStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _directory;
    private readonly string _profilesPath;
    private readonly string _sessionPath;

    public JsonProfileStore(string directory)
    {
        _directory = directory;
        _profilesPath = Path.Combine(directory, "profiles.json");
        _sessionPath = Path.Combine(directory, "profile-session.json");
    }

    public IReadOnlyList<AppProfile> LoadProfiles()
    {
        try
        {
            if (!File.Exists(_profilesPath)) return ProfileDefaults.BuiltIns();
            List<AppProfile>? list;
            try { list = JsonSerializer.Deserialize<List<AppProfile>>(File.ReadAllText(_profilesPath), Options); }
            catch (JsonException)
            {
                KeepCorruptCopy();
                return ProfileDefaults.BuiltIns();
            }
            if (list is null || list.Count == 0) return ProfileDefaults.BuiltIns();
            return list.Where(p => p is not null).Select(Clean).ToList();
        }
        catch
        {
            return ProfileDefaults.BuiltIns();
        }
    }

    private void KeepCorruptCopy()
    {
        try { File.Copy(_profilesPath, _profilesPath + ".corrupt", overwrite: true); }
        catch { }
    }

    public void SaveProfiles(IReadOnlyList<AppProfile> profiles) => Write(_profilesPath, () => JsonSerializer.Serialize(profiles, Options));

    public ProfileSession? LoadSession()
    {
        try
        {
            if (!File.Exists(_sessionPath)) return null;
            ProfileSession? s = JsonSerializer.Deserialize<ProfileSession>(File.ReadAllText(_sessionPath), Options);
            if (s is null || string.IsNullOrEmpty(s.ProfileId)) return null;
            return s with
            {
                ProfileName = s.ProfileName ?? "",
                StoppedServices = NonBlank(s.StoppedServices),
                ManagedServices = NonBlank(s.ManagedServices),
            };
        }
        catch
        {
            return null;
        }
    }

    public void SaveSession(ProfileSession? session)
    {
        if (session is not null)
        {
            Write(_sessionPath, () => JsonSerializer.Serialize(session, Options));
            return;
        }
        try { File.Delete(_sessionPath); } catch { }
    }

    private static AppProfile Clean(AppProfile p) => p with
    {
        Id = string.IsNullOrEmpty(p.Id) ? Guid.NewGuid().ToString("N") : p.Id,
        Name = p.Name ?? "",
        Description = p.Description ?? "",
        Trigger = p.Trigger ?? "",
        Apps = NonBlank(p.Apps),
        Services = NonBlank(p.Services),
    };

    private static IReadOnlyList<string> NonBlank(IReadOnlyList<string>? entries) =>
        entries is null ? Array.Empty<string>() : entries.Where(e => !string.IsNullOrWhiteSpace(e)).ToList();

    private void Write(string path, Func<string> json)
    {
        string temp = path + ".tmp";
        try
        {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(temp, json());
            File.Move(temp, path, overwrite: true);
        }
        catch
        {
            try { File.Delete(temp); } catch { }
        }
    }
}
