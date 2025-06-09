using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Net;


namespace LoadBalancer
{
    public class UDPLoad
    {
        UdpClient udpListener = new UdpClient(8888);
        IPEndPoint groupEP = new IPEndPoint(IPAddress.Any, 8888);


        public async void StartListeningAsync()
        {
            await Task.Run(() => ListenForClients());
        }

        private void ListenForClients()
        {
            while (true)
            {
                byte[] bytes = udpListener.Receive(ref groupEP);
                //Console.WriteLine(bytes);
                string request = Encoding.ASCII.GetString(bytes);

                if (request == "DISCOVER_LOAD")
                {
                    byte[] response = Encoding.ASCII.GetBytes("LOAD_IP:" + GetLocalIPAddress());
                    udpListener.Send(response, response.Length, groupEP); // gửi lại cho client
                }
            }
        }

        public static string GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
            throw new Exception("No network adapters with an IPv4 address in the system!");
        }
    }
}
