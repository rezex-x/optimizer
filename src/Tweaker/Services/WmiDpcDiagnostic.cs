using System;
using System.Globalization;
using System.Management;
using System.Threading;
using System.Threading.Tasks;

namespace Tweaker.Services;

public sealed class WmiDpcDiagnostic : IDpcDiagnostic
{
    private const int Samples = 3;

    public Task<DpcResult> SampleAsync(CancellationToken ct = default) => Task.Run(() =>
    {
        try
        {
            double dpcT = 0, intT = 0, dpcQ = 0, intR = 0;
            int n = 0;
            for (int i = 0; i < Samples; i++)
            {
                ct.ThrowIfCancellationRequested();
                if (TryReadOnce(out double a, out double b, out double c, out double d))
                {
                    dpcT += a; intT += b; dpcQ += c; intR += d; n++;
                }
                if (i < Samples - 1) Thread.Sleep(1000);
            }
            if (n == 0) return DpcResult.Empty;
            return new DpcResult(dpcT / n, intT / n, dpcQ / n, intR / n);
        }
        catch { return DpcResult.Empty; }
    });

    private static bool TryReadOnce(out double dpcTime, out double intTime, out double dpcQueued, out double intRate)
    {
        dpcTime = intTime = dpcQueued = intRate = 0;
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT PercentDPCTime, PercentInterruptTime, DPCsQueuedPersec, InterruptsPersec FROM Win32_PerfFormattedData_PerfOS_Processor WHERE Name='_Total'");
            using var results = searcher.Get();
            foreach (ManagementObject mo in results)
            {
                using (mo)
                {
                    dpcTime = ToD(mo["PercentDPCTime"]);
                    intTime = ToD(mo["PercentInterruptTime"]);
                    dpcQueued = ToD(mo["DPCsQueuedPersec"]);
                    intRate = ToD(mo["InterruptsPersec"]);
                    return true;
                }
            }
            return false;
        }
        catch { return false; }
    }

    private static double ToD(object? v) => v is null ? 0 : Convert.ToDouble(v, CultureInfo.InvariantCulture);
}
