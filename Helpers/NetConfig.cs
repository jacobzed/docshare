using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace DocShare.Helpers
{
    public class NetConfig
    {
        /// <summary>
        /// Get all local IPv4 addresses
        /// </summary>
        public IEnumerable<string> GetLocalIPs()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            return host.AddressList
                .Where(ip => ip.AddressFamily == AddressFamily.InterNetwork)
                .Select(ip => ip.ToString());
        }

        /// <summary>
        /// Guess mostly likely local network IPv4 address.
        /// </summary>
        public string? GetLocalIP()
        {
            var nics = NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => !ni.Description.Contains("Virtual Ethernet"))
                .Where(ni => ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet || ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
                .Where(ni => ni.OperationalStatus == OperationalStatus.Up);

            foreach (var nic in nics)
            {
                var ip = nic.GetIPProperties().UnicastAddresses
                    .Where(ip => ip.Address.AddressFamily == AddressFamily.InterNetwork)
                    .Select(ip => ip.Address.ToString())
                    .FirstOrDefault();

                return ip;
            }

            return null;
        }
    }
}
