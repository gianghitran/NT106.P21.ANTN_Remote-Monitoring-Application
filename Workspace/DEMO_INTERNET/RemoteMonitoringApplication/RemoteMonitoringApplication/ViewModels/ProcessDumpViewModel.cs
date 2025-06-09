using RemoteMonitoringApplication.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using RemoteMonitoringApplication.Services;
using System.Diagnostics;

namespace RemoteMonitoringApplication.ViewModels
{
    public class ProcessDumpViewModel
    {

        public string ProcessDump(string PIDget,string savepath)
        {
            try
            {
                string PIDText = PIDget;
                int PID = PIDText.Length > 0 ? int.Parse(PIDText) : -1;
                if (PID < 0)
                {
                    System.Windows.MessageBox.Show("Please enter a valid PID.", "Invalid PID", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return "In valid PID";
                }
                byte[] dumpData = ProcessDumpService.CreateProcessDumpWithProcDump(PID);

                string timestamp = DateTime.Now.ToString("dd-MM-yyyy_HH-mm-ss");
                string filename = $"dump_{PID}_{timestamp}.dmp";
                string fullPath = System.IO.Path.Combine(savepath, filename);
                ProcessDumpService.SaveDumpToFile(dumpData, fullPath);
                Console.WriteLine("Dump data length: " + dumpData.Length + $"saved at {fullPath}");
                return ("Dump data length: " + dumpData.Length + $"saved at {fullPath}");
            }
            catch (Exception ex)
            {
                
                    Console.WriteLine($"Errror: {ex}");
                    System.Windows.MessageBox.Show($"Errror: {ex}");
                return null;
                
            }
                //ProcessDumpService.SaveDumpToFile(dumpData, "C:/Users/ASUS/Documents/Nam2_Ki2/ltmcb/DoAn/Savedata/dump.dmp");
            }
       
    }

}
