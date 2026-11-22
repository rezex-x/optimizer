using System;
using System.Globalization;
using Microsoft.Win32;
using Tweaker.Core.Services;

namespace Tweaker.Services;

public sealed class InterruptSettings
{
    private const string Hklm = "HKLM";
    private readonly IRegistryService _registry;

    public InterruptSettings(IRegistryService registry) => _registry = registry;

    public MsiMode ReadMsi(string instanceId)
    {
        try
        {
            (bool exists, _, object? value) = _registry.Read(Hklm, InterruptRules.MsiPath(instanceId), "MSISupported");
            if (!exists || value is null) return MsiMode.Default;
            return Convert.ToInt32(value, CultureInfo.InvariantCulture) == 0 ? MsiMode.Off : MsiMode.On;
        }
        catch
        {
            return MsiMode.Default;
        }
    }

    public string? WriteMsi(string instanceId, MsiMode mode)
    {
        try
        {
            string path = InterruptRules.MsiPath(instanceId);
            switch (mode)
            {
                case MsiMode.On: _registry.Set(Hklm, path, "MSISupported", RegistryValueKind.DWord, 1); break;
                case MsiMode.Off: _registry.Set(Hklm, path, "MSISupported", RegistryValueKind.DWord, 0); break;
                default: _registry.DeleteValue(Hklm, path, "MSISupported"); break;
            }
            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    public (int Policy, ulong Mask) ReadAffinity(string instanceId)
    {
        try
        {
            string path = InterruptRules.AffinityPath(instanceId);
            (bool exists, _, object? policy) = _registry.Read(Hklm, path, "DevicePolicy");
            if (!exists || policy is null) return (InterruptRules.PolicyDefault, 0);
            (bool hasMask, _, object? mask) = _registry.Read(Hklm, path, "AssignmentSetOverride");
            return (Convert.ToInt32(policy, CultureInfo.InvariantCulture), hasMask ? InterruptRules.MaskFrom(mask as byte[]) : 0);
        }
        catch
        {
            return (InterruptRules.PolicyDefault, 0);
        }
    }

    public string? WriteAffinity(string instanceId, int policy, ulong mask)
    {
        try
        {
            string path = InterruptRules.AffinityPath(instanceId);
            if (policy <= 0)
            {
                _registry.DeleteValue(Hklm, path, "DevicePolicy");
                _registry.DeleteValue(Hklm, path, "AssignmentSetOverride");
                return null;
            }
            _registry.Set(Hklm, path, "DevicePolicy", RegistryValueKind.DWord, policy);
            if (policy == InterruptRules.PolicySpecified)
                _registry.Set(Hklm, path, "AssignmentSetOverride", RegistryValueKind.Binary, InterruptRules.MaskBytes(mask));
            else
                _registry.DeleteValue(Hklm, path, "AssignmentSetOverride");
            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }
}
