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

        public void ProcessDump(System.Windows.Controls.TextBox Textbox_PID)
        {
            string PIDText = Textbox_PID.Text.Trim();
            int PID = PIDText.Length > 0 ? int.Parse(PIDText) : -1;
            if (PID < 0)
            {
                System.Windows.MessageBox.Show("Please enter a valid PID.", "Invalid PID", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            byte[] dumpData = ProcessDumpService.CreateProcessDumpWithProcDump(PID);
            ProcessDumpService.SaveDumpToFile(dumpData, "C:/Users/ASUS/Documents/Nam2_Ki2/ltmcb/DoAn/Savedata");
        }
    }

}
