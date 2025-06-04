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

        public ProcessList getProcessList()
        {
            ProcessList processList = new ProcessList
            {
                RealTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                ProcessInfo = new List<ProcessInfo>()
            };

            var recentProcesses = Process.GetProcesses()
                                    .OrderByDescending(p => p.Id)
                                    .Take(30);

            foreach (var process in recentProcesses)
            {
                try
                {
                    Console.WriteLine($"PID: {process.Id} | Tên: {process.ProcessName}");

                    // RAM
                    Console.WriteLine($"RAM: {process.WorkingSet64 / 1024} KB");

                    // CPU
                    var cpuCounter = new PerformanceCounter("Process", "% Processor Time", process.ProcessName, true);
                    cpuCounter.NextValue();
                    Thread.Sleep(100);
                    float cpuUsage = cpuCounter.NextValue() / Environment.ProcessorCount;
                    Console.WriteLine($"CPU: {cpuUsage:0.00}%");

                    // Disk I/O
                    var diskReadCounter = new PerformanceCounter("Process", "IO Read Bytes/sec", process.ProcessName);
                    var diskWriteCounter = new PerformanceCounter("Process", "IO Write Bytes/sec", process.ProcessName);
                    Console.WriteLine($"Disk Read: {diskReadCounter.NextValue() / 1024:0.00} KB/s");
                    Console.WriteLine($"Disk Write: {diskWriteCounter.NextValue() / 1024:0.00} KB/s");

                    Console.WriteLine(new string('-', 50));

                    ProcessInfo processInfo = new ProcessInfo
                    {
                        PID = process.Id,
                        ProcessName = process.ProcessName,
                        CPU = $"{cpuUsage:0.00}%",
                        Memory = $"{process.WorkingSet64 / 1024} KB",
                        DiskRead = $"{diskReadCounter.NextValue() / 1024:0.00} KB/s",
                        DiskWrite = $"{diskWriteCounter.NextValue() / 1024:0.00} KB/s"
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
