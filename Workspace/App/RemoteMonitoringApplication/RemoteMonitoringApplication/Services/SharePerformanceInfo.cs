using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;

using System.Windows;
using RemoteMonitoringApplication.ViewModels;


namespace RemoteMonitoringApplication.Services
{
    class SharePerformanceInfo
    {
        private SystemMonitorViewModel _viewModel = new();
        public void ShowDiskInfo(System.Windows.Controls.RichTextBox TextBoxDetails)
        {
            TextBoxDetails.Document.Blocks.Clear();

            TextBoxDetails.AppendText("Caption\tFreeSpace\tSize\n");
            var DiskIn4 = _viewModel.diskInfo(_viewModel.FetchDiskInfo());

            foreach (var drive in DiskIn4)
            {
                TextBoxDetails.AppendText($"\n{drive.Caption}\t{drive.FreeSpace}\t{drive.Size}");
            }
            Run Text = new Run("\nMORE INFORMATIONS:\n");
            Text.FontWeight = FontWeights.Bold;
            string moreDiskIn4 = _viewModel.fetchIn4("wmic diskdrive get Name,Model,Size,Status\r\n");

            string partitionin4 = _viewModel.fetchIn4("wmic partition get Name,Size,Type\r\n");
            Run Text1 = new Run("Disk drive information:\n");
            Text1.FontWeight = FontWeights.Bold;
            Run Text2 = new Run("Partition information:\n");
            Text2.FontWeight = FontWeights.Bold;

            Paragraph Full = new Paragraph();
            Full.Inlines.Add(Text);
            Full.Inlines.Add(Text1);
            Full.Inlines.Add(moreDiskIn4);
            Full.Inlines.Add(Text2);
            Full.Inlines.Add(partitionin4);
            TextBoxDetails.Document.Blocks.Add(Full);
        }
        public void GetDiskInfo()
        {
            var DiskIn4 = _viewModel.diskInfo(_viewModel.FetchDiskInfo());

        }
    }
}
