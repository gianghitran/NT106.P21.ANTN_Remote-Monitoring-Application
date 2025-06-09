using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Diagnostics;
//public class ProcessInfo
//{
//    public string ProcessName { get; set; }
//    public string Status { get; set; }
//    public string CPU { get; set; }
//    public string Memory { get; set; }
//    public string Disk { get; set; }
//    public string NetWork { get; set; }
//}

namespace RemoteMonitoringApplication.Services
{
    class SystemMonitorService
    {
        public string RunCMD(string command)
        {
            ProcessStartInfo procStartInfo = new ProcessStartInfo("cmd", "/c " + command)
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using Process proc = new() { StartInfo = procStartInfo };
            proc.Start();
            string result = proc.StandardOutput.ReadToEnd();
            Console.WriteLine($"Get info from client:\n{result}");

            return result;
        }
    }
}
