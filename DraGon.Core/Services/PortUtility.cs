using System.Net.NetworkInformation;

namespace DraGon.Core.Services;

public static class PortUtility
{
    public static bool IsTcpPortInUse(int port)
    {
        if (port is < 1 or > 65535)
        {
            return false;
        }

        try
        {
            var ipProperties = IPGlobalProperties.GetIPGlobalProperties();
            var endpoints = ipProperties.GetActiveTcpListeners();
            return endpoints.Any(ep => ep.Port == port);
        }
        catch
        {
            return false;
        }
    }

    public static HashSet<int> GetActiveListeningPorts()
    {
        try
        {
            var ipProperties = IPGlobalProperties.GetIPGlobalProperties();
            return ipProperties.GetActiveTcpListeners()
                .Select(ep => ep.Port)
                .ToHashSet();
        }
        catch
        {
            return [];
        }
    }
}
