using RemoteMonitoringApplication.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace RemoteMonitoringApplication.ViewModels
{
    public class DriveInfoModel
    {
        public string Caption { get; set; }
        public string FreeSpace { get; set; }
        public string Size { get; set; }
    }
    public class SystemMonitorViewModel : INotifyPropertyChanged
    {
        private readonly SystemMonitorService _service = new();

        private ObservableCollection<DriveInfoModel> _drives = new();
        public ObservableCollection<DriveInfoModel> Drives
        {
            get => _drives;
            set
            {
                _drives = value;
                OnPropertyChanged(nameof(Drives));
            }
        }

        public string FetchDiskInfo()
        {
            string output = _service.RunCMD("wmic logicaldisk get size,freespace,caption");
            Drives = ParseOutput(output);
            //Console.WriteLine("Caption\tFreeSpace\tSize");

            //foreach (var drive in Drives)
            //{
            //    Console.WriteLine($"{drive.Caption}\t{drive.FreeSpace}\t{drive.Size}");
            //}
            return output;
        }
        public string fetchIn4(string cmd)
        {
            string rawOutput = _service.RunCMD(cmd);
            
            return rawOutput;
        }


        public ObservableCollection<DriveInfoModel> diskInfo(string drawIn4)
        {
            var Drives = new ObservableCollection<DriveInfoModel>();
            Drives = ParseOutput(drawIn4);
            return Drives;
        }
        public string FetchCPUInfo()
        {
            string output = _service.RunCMD("wmic cpu get Name,MaxClockSpeed");
            Drives = ParseOutput(output);
            //Console.WriteLine("Caption\tFreeSpace\tSize");

            //foreach (var drive in Drives)
            //{
            //    Console.WriteLine($"{drive.Caption}\t{drive.FreeSpace}\t{drive.Size}");
            //}
            return output;

        }
        public string FetchGPUInfo()
        {
            string output = _service.RunCMD("wmic path win32_VideoController get Name,AdapterRAM,DriverVersion");
            Drives = ParseOutput(output);
            //Console.WriteLine("Caption\tFreeSpace\tSize");

            //foreach (var drive in Drives)
            //{
            //    Console.WriteLine($"{drive.Caption}\t{drive.FreeSpace}\t{drive.Size}");
            //}
            return output;

        }

        public string FetchMemoryInfo()
        {
            string output = _service.RunCMD("wmic OS get FreePhysicalMemory,TotalVisibleMemorySize");
            Drives = ParseOutput(output);
            //Console.WriteLine("Caption\tFreeSpace\tSize");

            //foreach (var drive in Drives)
            //{
            //    Console.WriteLine($"{drive.Caption}\t{drive.FreeSpace}\t{drive.Size}");
            //}
            return output;
        }
        private ObservableCollection<DriveInfoModel> ParseOutput(string output)
        {
            var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var result = new ObservableCollection<DriveInfoModel>();

            foreach (var line in lines.Skip(1)) // Skip header
            {
                var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 3)
                {
                    result.Add(new DriveInfoModel
                    {
                        Caption = parts[0],
                        FreeSpace = parts[1],
                        Size = parts[2]
                    });
                }
            }

            return result;
        }
        public string[] FetchAllInfo()
        {
            string CPU = FetchCPUInfo();
            string GPU = FetchGPUInfo();
            string memo = FetchMemoryInfo();
            string Disk = FetchDiskInfo();
            return new string[] { CPU, GPU, memo, Disk };
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }


}
