using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace RemoteMonitoringApplication.Services
{
    class ProcessDumpService
    {
        public static byte[] CreateProcessDumpWithProcDump(int processId)
        {
            if (processId == Process.GetCurrentProcess().Id)
            {
                System.Windows.Forms.MessageBox.Show("Cannot dump this monitor process!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                throw new InvalidOperationException("Cannot dump this monitor process");
            }
            if (!Process.GetProcesses().Any(p => p.Id == processId))
            {
                System.Windows.Forms.MessageBox.Show("Process not found!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                throw new InvalidOperationException("Process not found!");
            }


            string procdumpExePath = @"procdump.exe"; // đường dẫn đến procdump.exe
            if (!File.Exists(procdumpExePath))
                throw new FileNotFoundException("Không tìm thấy procdump.exe.");

            string tempDumpPath = Path.GetTempFileName(); // file tạm

            // Tạo tiến trình chạy procdump
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = procdumpExePath,
                Arguments = $"-ma {processId} \"{tempDumpPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            try
            {
                var proc = Process.Start(psi);
                proc.WaitForExit();

                if (!File.Exists(tempDumpPath) || new FileInfo(tempDumpPath).Length == 0)
                {
                    System.Windows.Forms.MessageBox.Show("Failed to create dump file!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return null;
                }

                byte[] dumpData = File.ReadAllBytes(tempDumpPath);
                File.Delete(tempDumpPath);

                Console.WriteLine($"Dump file created: {dumpData.Length} bytes");
                return dumpData;
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show($"Error while creating dump: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null;
            }
        }


    
    public static void SaveDumpToFile(byte[] dumpData, string filePath)
        {
            
            if (dumpData == null || dumpData.Length == 0)
            {
                System.Windows.Forms.MessageBox.Show("No data!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            File.WriteAllBytes(filePath, dumpData);
            Console.WriteLine($"File was saved at: {filePath}");
            System.Windows.Forms.MessageBox.Show($"File was saved at: {filePath}", "Notification", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
    }
}

