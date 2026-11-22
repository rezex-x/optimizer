using System.Collections.Generic;
using System.Management;

namespace Tweaker.Services;

public sealed class WmiGpuDetector : IGpuDetector
{
    public GpuInfo Detect()
    {
        try
        {
            var names = new List<string>();
            using var searcher = new ManagementObjectSearcher(
                "SELECT Name, AdapterCompatibility FROM Win32_VideoController");
            using var results = searcher.Get();
            foreach (ManagementObject mo in results)
            {
                using (mo)
                {
                    names.Add(mo["Name"]?.ToString() ?? "");
                    names.Add(mo["AdapterCompatibility"]?.ToString() ?? "");
                }
            }
            return GpuVendors.From(names);
        }
        catch
        {
            return new GpuInfo(false, false, false, new List<string>());
        }
    }
}
