using RemoteMonitoringApplication.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using RemoteMonitoringApplication.Services;

namespace RemoteMonitoringApplication.ViewModels
{
    public class ProcessDumpViewModel
    {

        public void ProcessDump(string PIDget)
        {
            string PIDText = PIDget;
            int PID = PIDText.Length > 0 ? int.Parse(PIDText) : -1;
            if (PID < 0)
            {
                System.Windows.MessageBox.Show("Please enter a valid PID.", "Invalid PID", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            byte[] dumpData = ProcessDumpService.CreateProcessDumpWithProcDump(PID);
            ProcessDumpService.SaveDumpToFile(dumpData, "dumpTemp.dmp");
            Console.WriteLine("Dump data length: " + dumpData.Length + "saved at ./dumpTemp.dmp");
            return;

            //ProcessDumpService.SaveDumpToFile(dumpData, "C:/Users/ASUS/Documents/Nam2_Ki2/ltmcb/DoAn/Savedata/dump.dmp");
        }
        public byte[] ProcessDumpFile(string PIDget)
        {
            string PIDText = PIDget;
            int PID = PIDText.Length > 0 ? int.Parse(PIDText) : -1;
            if (PID < 0)
            {
                System.Windows.MessageBox.Show("Please enter a valid PID.", "Invalid PID", MessageBoxButton.OK, MessageBoxImage.Warning);
                return null;
            }
            byte[] dumpData = ProcessDumpService.CreateProcessDumpWithProcDump(PID);
            ProcessDumpService.SaveDumpToFile(dumpData, "dumpTemp.dmp");
            Console.WriteLine("Dump data length: " + dumpData.Length + "saved at ./dumpTemp.dmp");
            return dumpData;

            //ProcessDumpService.SaveDumpToFile(dumpData, "C:/Users/ASUS/Documents/Nam2_Ki2/ltmcb/DoAn/Savedata/dump.dmp");
        }
    }

}
