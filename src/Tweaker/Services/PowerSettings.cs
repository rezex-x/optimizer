using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Tweaker.Services;

public interface IPowerSettings
{
    Guid? ActiveScheme();

    uint? ReadAc(Guid scheme, Guid subgroup, Guid setting);

    uint? ReadAcDefault(Guid scheme, Guid subgroup, Guid setting);

    string? WriteAc(Guid scheme, Guid subgroup, Guid setting, uint value);
}

public static class PowerKeys
{
    public static string Key(Guid scheme, Guid subgroup, Guid setting) =>
        scheme.ToString("D") + "|" + subgroup.ToString("D") + "|" + setting.ToString("D");
}

public sealed class PowerSettings : IPowerSettings
{
    public Guid? ActiveScheme()
    {
        IntPtr pointer = IntPtr.Zero;
        try
        {
            if (PowerGetActiveScheme(IntPtr.Zero, out pointer) != 0 || pointer == IntPtr.Zero) return null;
            return Marshal.PtrToStructure<Guid>(pointer);
        }
        catch
        {
            return null;
        }
        finally
        {
            if (pointer != IntPtr.Zero) LocalFree(pointer);
        }
    }

    public uint? ReadAc(Guid scheme, Guid subgroup, Guid setting)
    {
        try
        {
            return PowerReadACValueIndex(IntPtr.Zero, ref scheme, ref subgroup, ref setting, out uint value) == 0 ? value : null;
        }
        catch
        {
            return null;
        }
    }

    public uint? ReadAcDefault(Guid scheme, Guid subgroup, Guid setting)
    {
        try
        {
            return PowerReadACDefaultIndex(IntPtr.Zero, ref scheme, ref subgroup, ref setting, out uint value) == 0 ? value : null;
        }
        catch
        {
            return null;
        }
    }

    public string? WriteAc(Guid scheme, Guid subgroup, Guid setting, uint value)
    {
        try
        {
            uint code = PowerWriteACValueIndex(IntPtr.Zero, ref scheme, ref subgroup, ref setting, value);
            if (code != 0) return new Win32Exception((int)code).Message;
            if (ActiveScheme() != scheme) return null;
            code = PowerSetActiveScheme(IntPtr.Zero, ref scheme);
            return code == 0 ? null : new Win32Exception((int)code).Message;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    [DllImport("powrprof.dll")]
    private static extern uint PowerGetActiveScheme(IntPtr userRootPowerKey, out IntPtr activePolicyGuid);

    [DllImport("powrprof.dll")]
    private static extern uint PowerReadACValueIndex(IntPtr rootPowerKey, ref Guid schemeGuid, ref Guid subGroupOfPowerSettingsGuid,
        ref Guid powerSettingGuid, out uint acValueIndex);

    [DllImport("powrprof.dll")]
    private static extern uint PowerReadACDefaultIndex(IntPtr rootPowerKey, ref Guid schemePersonalityGuid,
        ref Guid subGroupOfPowerSettingsGuid, ref Guid powerSettingGuid, out uint acDefaultIndex);

    [DllImport("powrprof.dll")]
    private static extern uint PowerWriteACValueIndex(IntPtr rootPowerKey, ref Guid schemeGuid, ref Guid subGroupOfPowerSettingsGuid,
        ref Guid powerSettingGuid, uint acValueIndex);

    [DllImport("powrprof.dll")]
    private static extern uint PowerSetActiveScheme(IntPtr userRootPowerKey, ref Guid schemeGuid);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr memory);
}
