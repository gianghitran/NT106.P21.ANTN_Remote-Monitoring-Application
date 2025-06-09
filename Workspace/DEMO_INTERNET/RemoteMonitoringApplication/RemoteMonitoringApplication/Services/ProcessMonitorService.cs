using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RemoteMonitoringApplication.ViewModels;

namespace RemoteMonitoringApplication.Services
{

    public class ProcessMonitorService
    {

        public ProcessList getProcessList(byte[] Sharekey,string IV)
        {
            ProcessList processList = new ProcessList
            {
                RealTime = CryptoService.Encrypt(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),Sharekey,IV),
                ProcessInfo = new List<ProcessInfo>()
            };

            var recentProcesses = Process.GetProcesses()
                                    .OrderByDescending(p => p.Id)
                                    .Take(30);

            foreach (var process in recentProcesses)
            {
                try
                {
                    Console.WriteLine($"PID getting... | Name getting...");

                    // RAM
                    Console.WriteLine($"RAM getting...");

                    // CPU
                    var cpuCounter = new PerformanceCounter("Process", "% Processor Time", process.ProcessName, true);
                    cpuCounter.NextValue();
                    Thread.Sleep(100);
                    float cpuUsage = cpuCounter.NextValue() / Environment.ProcessorCount;
                    Console.WriteLine($"CPU getting...");

                    // Disk I/O
                    var diskReadCounter = new PerformanceCounter("Process", "IO Read Bytes/sec", process.ProcessName);
                    var diskWriteCounter = new PerformanceCounter("Process", "IO Write Bytes/sec", process.ProcessName);
                    Console.WriteLine($"Disk Read getting...");
                    Console.WriteLine($"Disk Write getting...");

                    Console.WriteLine(new string('-', 50));

                    ProcessInfo processInfo = new ProcessInfo
                    {
                        PID = process.Id,
                        ProcessName = CryptoService.Encrypt(process.ProcessName,Sharekey,IV),
                        CPU = CryptoService.Encrypt( $"{cpuUsage:0.00}%", Sharekey,IV),
                        Memory = CryptoService.Encrypt($"{process.WorkingSet64 / 1024} KB", Sharekey,IV),
                        DiskRead = CryptoService.Encrypt($"{diskReadCounter.NextValue() / 1024:0.00} KB/s",Sharekey,IV),
                        DiskWrite = CryptoService.Encrypt($"{diskWriteCounter.NextValue() / 1024:0.00} KB/s", Sharekey,IV)
                    };
                    processList.ProcessInfo.Add(processInfo);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Get process ERROR: {ex.Message}");
                }
            }
            return processList;
        }
    }
}
