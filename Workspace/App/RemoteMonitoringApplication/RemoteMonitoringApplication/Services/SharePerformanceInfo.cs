using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;

using System.Windows;
using RemoteMonitoringApplication.ViewModels;
using Org.BouncyCastle.Math;
using System.Windows.Controls;



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
        public async void showDiskBar(List<DriveDiskModel> disk, System.Windows.Controls.ProgressBar diskBar, TextBlock diskText)
        {
            double freeSpace = 0;
            double size = 0;
           for (int i = 0; i < disk.Count; i++)
            {
                freeSpace += double.Parse(disk[i].FreeSpace);
                size += double.Parse(disk[i].Size);
            }
            
           

            double used = 100 - (freeSpace / size * 100);

            diskBar.Value = 0;
            for (double i = 0; i <= used; i++)
            {
                diskBar.Value = i;
                diskText.Text = $"{i}%";

                await Task.Delay(50);
            }
        }
        public async void showMemoryBar(List<DriveMemoryModel> memory, System.Windows.Controls.ProgressBar memoryBar, TextBlock memoryText)
        {
            double freeSpace = 0;
            double size = 0;
            for (int i = 0; i < memory.Count; i++)
            {
                freeSpace += double.Parse(memory[i].FreeSpace);
                size += double.Parse(memory[i].Size);
            }
            double used = 100 - (freeSpace / size * 100);

            memoryBar.Value = 0;
            for (double i = 0; i <= used; i++)
            {
                memoryBar.Value = i;
                memoryText.Text = $"{i}%";

                await Task.Delay(50);
            }
        }

        public async void showCPUBar(List<DriveCPUModel> cpu, System.Windows.Controls.ProgressBar cpuBar, TextBlock cpuText)
        {
            double used = 0;
            for (int i = 0; i < cpu.Count; i++)
            {
                used += double.Parse(cpu[i].Used);
            }
            cpuBar.Value = 0;
            for (double i = 0; i <= used; i++)
            {
                cpuBar.Value = i;
                cpuText.Text = $"{i}%";

                await Task.Delay(50);
            }
        }
    }
}
