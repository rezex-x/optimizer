using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Threading;
using Tweaker.Core.Services;
using Tweaker.Services;
using Tweaker.ViewModels;

namespace Tweaker.Composition;

public static class AppServices
{
    public static Tweaker.Shell.ShellViewModel BuildShell()
    {
        Dispatcher ui = Dispatcher.CurrentDispatcher;
        string baseDir = AppContext.BaseDirectory;
        string backupDir = System.IO.Path.Combine(baseDir, "backups");

        var registry = new RegistryService();
        var backup = new BackupService(registry, backupDir);
        var runner = new ProcessRunner();
        var batch = new BatchService(runner);
        var detector = new RegistryStateDetector(registry);
        var applied = new AppliedTweakStore(System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Tweaker", "applied-tweaks.json"));
        var engine = new TweakEngine(registry, backup, batch, detector, applied);
        var confirm = new DialogConfirmationService();

        var activity = new ActivityLog(System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Tweaker", "activity.log"));
        var pending = new PendingChangesViewModel(activity.For("Changes"));

        var catalog = new CatalogService(CatalogSourceFactory.Create());
        var tweaks = new Tweaker.ViewModels.MainViewModel(catalog, engine, confirm,
            new Tweaker.Services.PowerSettings(),
            new Tweaker.Services.JsonPowerBackup(System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Tweaker", "power-backup.json")),
            pending, activity, appx: new AppxService(runner));

        var freed = new FreedResourcesViewModel(() => tweaks.EstimatedFreedMb, () => tweaks.CoveredServices);
        bool freedRefreshPending = false;
        void ScheduleFreedRefresh()
        {
            if (freedRefreshPending) return;
            freedRefreshPending = true;
            ui.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                freedRefreshPending = false;
                freed.Refresh();
            }));
        }
        tweaks.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Tweaker.ViewModels.MainViewModel.EstimatedFreedMb)) ScheduleFreedRefresh();
        };

        var install = new Tweaker.ViewModels.InstallViewModel(new WingetService(runner), activity,
            detector: new InstalledAppDetector(runner),
            store: new JsonInstalledStore(System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Tweaker", "installed.json")));
        var configTweaks = catalog.Tweaks
            .Where(t => Tweaker.Core.Catalog.ConfigSurface.IsConfig(t.Category))
            .ToList();
        var config = new Tweaker.ViewModels.ConfigViewModel(configTweaks, engine, confirm, pending, activity);
        config.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Tweaker.ViewModels.ConfigViewModel.AppliedCount)) ScheduleFreedRefresh();
        };
        var updates = new Tweaker.ViewModels.UpdatesViewModel(new WindowsUpdateService(registry, backup, runner), activity);

        string nvidiaBackupPath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Tweaker", "nvidia-backup.json");
        var nvidia = new Tweaker.ViewModels.NvidiaViewModel(
            new Tweaker.Services.NvApiWrapperSettingsService(),
            new Tweaker.Core.Nvidia.NvidiaBackupStore(nvidiaBackupPath),
            confirm,
            Tweaker.Core.Nvidia.NvidiaSettingsCatalog.Items(),
            pending, activity);

        var gpu = new WmiGpuDetector().Detect();
        var performance = new Tweaker.ViewModels.PerformanceViewModel(
            new Tweaker.Services.LibreHardwareMonitorSource(),
            new Tweaker.Services.WmiSystemInfoSource(),
            new Tweaker.ViewModels.DpcSampleViewModel(new Tweaker.Services.WmiDpcDiagnostic()),
            new Tweaker.Services.DriveInfoVolumeSource());

        var network = new Tweaker.ViewModels.NetworkViewModel(
            new Tweaker.Services.NetworkInterfaceAdapterSource(),
            new Tweaker.Services.IpHelperConnectionSource(),
            new Tweaker.Services.PingNetworkDiagnostic());

        string windowsDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var companies = new Tweaker.Services.FileCompanyLookup();
        var profileManager = new Tweaker.ViewModels.ProfileManager(
            new Tweaker.Services.JsonProfileStore(System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Tweaker")),
            new Tweaker.Services.WindowsProcessSource(),
            new Tweaker.Services.WmiServiceSource(runner, windowsDir),
            new Tweaker.Services.WindowsProcessCloser(),
            new Tweaker.Services.RunningExeSource(),
            Environment.ProcessId,
            windowsDir,
            companies.CompanyOf);
        var profiles = new Tweaker.ViewModels.ProfilesViewModel(profileManager);

        var processes = new Tweaker.ViewModels.ProcessesViewModel(
            new Tweaker.Services.WindowsProcessSource(),
            new Tweaker.Services.PdhGpuUsageSource(),
            Environment.ProcessId,
            windowsDir,
            profiles: profileManager,
            freed: freed);

        var services = new Tweaker.ViewModels.ServicesViewModel(
            new Tweaker.Services.WmiServiceSource(runner, windowsDir), profiles: profileManager,
            freed: freed, workingSetOf: WorkingSetOf);

        var resources = new Tweaker.ViewModels.ResourcesViewModel(profiles, performance, network, processes, services);

        var hardware = new Tweaker.ViewModels.HardwareViewModel(
            new Tweaker.Services.WmiDeviceSource(),
            new Tweaker.Services.JsonDevicePowerBackup(System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Tweaker", "device-power-backup.json")),
            new Tweaker.ViewModels.StorageViewModel(new Tweaker.Services.WmiStorageDiagnostic()),
            advanced: new Tweaker.ViewModels.DeviceAdvancedViewModel(
                new Tweaker.Services.InterruptSettings(registry),
                new Tweaker.Services.WmiNicPropertySource(runner),
                new Tweaker.Services.JsonAdvancedBackup(System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Tweaker", "advanced-backup.json")),
                Environment.ProcessorCount));

        var inspector = new Tweaker.ViewModels.InspectorViewModel(
            new Tweaker.Services.ConfigFileStore(),
            new Tweaker.Services.RunningExeSource(),
            () => Tweaker.Services.ConfigDetector.Find(Environment.GetEnvironmentVariable,
                d => System.IO.Directory.EnumerateDirectories(d), System.IO.File.Exists, SteamPath()),
            p => Tweaker.Services.ConfigDetector.Display(p, Environment.GetEnvironmentVariable));

        activity.Watch(profiles, "App Profiles");
        activity.Watch(processes, "Processes");
        activity.Watch(services, "Services");
        activity.Watch(hardware, "Hardware");
        if (hardware.Advanced is not null) activity.Watch(hardware.Advanced, "Hardware");
        activity.Watch(inspector, "Inspector");

        return new Tweaker.Shell.ShellViewModel(tweaks, install, config, updates, nvidia, gpu, resources, hardware, inspector,
            pending, activity, freed: freed);
    }

    private static long? WorkingSetOf(int pid)
    {
        try
        {
            using Process p = Process.GetProcessById(pid);
            return p.PrivateMemorySize64;
        }
        catch
        {
            return null;
        }
    }

    private static string? SteamPath()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
            return (key?.GetValue("SteamPath") as string)?.Replace('/', '\\');
        }
        catch
        {
            return null;
        }
    }
}
