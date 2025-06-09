using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace RemoteMonitoringApplication.Services
{
    class BroadcastLANServer
    {
        private UdpClient udpClient = new UdpClient();
        private IPEndPoint ep = new IPEndPoint(IPAddress.Broadcast, 8888);

        public BroadcastLANServer()
        {
            udpClient.EnableBroadcast = true;
        }

        public async Task<string> DiscoverServer()
        {
            Console.WriteLine("Finding load balance on LAN...");
            // Gửi gói tin broadcast
            byte[] data = Encoding.ASCII.GetBytes("DISCOVER_LOAD");
            udpClient.Send(data, data.Length, ep);

            udpClient.Client.ReceiveTimeout = 3000;
            try
            {
                var serverEP = new IPEndPoint(IPAddress.Any, 0);
                byte[] response = udpClient.Receive(ref serverEP);
                string msg = Encoding.ASCII.GetString(response);

                if (msg.StartsWith("LOAD_IP:"))
                {
                    string serverIp = msg.Substring("LOAD_IP:".Length);
                    Console.WriteLine("Found load IP: " + serverIp);
                    return serverIp;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Cannot found load: " + ex.Message);
            }
            return null;
        }
    }
}
