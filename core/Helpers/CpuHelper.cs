using System.Management;

namespace core.Helpers;
// fix cpu usage info display wrong
public class CpuHelper
{
    public static string GetProcessorCoreName()
    {
        string ComputerName = "localhost";
        ManagementScope Scope;
        string? cpuName = String.Empty;
        Scope = new ManagementScope(String.Format("\\\\{0}\\root\\CIMV2", ComputerName), null);
        Scope.Connect();
        ObjectQuery Query = new("SELECT Name FROM Win32_Processor");
        ManagementObjectSearcher Searcher = new(Scope, Query);
        foreach (ManagementObject WmiObject in Searcher.Get())
        {
            cpuName = WmiObject["Name"].ToString();
        }
        return cpuName ?? "Unknown CPU";
    }
}
