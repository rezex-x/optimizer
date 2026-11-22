using System;
using System.Collections.Generic;
using System.IO;

namespace Tweaker.Services;

public sealed record VolumeInfo(string Name, string Label, long TotalBytes, long FreeBytes);

public interface IVolumeSource
{
    IReadOnlyList<VolumeInfo> Read();
}

public sealed class DriveInfoVolumeSource : IVolumeSource
{
    public IReadOnlyList<VolumeInfo> Read()
    {
        var result = new List<VolumeInfo>();
        try
        {
            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                try
                {
                    if (drive.DriveType != DriveType.Fixed || !drive.IsReady) continue;
                    string name = drive.Name.TrimEnd('\\');
                    result.Add(new VolumeInfo(name, LabelOf(drive), drive.TotalSize, drive.TotalFreeSpace));
                }
                catch
                {
                }
            }
        }
        catch
        {
            return Array.Empty<VolumeInfo>();
        }
        return result;
    }

    private static string LabelOf(DriveInfo drive)
    {
        try { return drive.VolumeLabel ?? ""; }
        catch { return ""; }
    }
}
