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
    public class DriveDiskModel
    {
        public string Caption { get; set; }
        public string FreeSpace { get; set; }
        public string Size { get; set; }
    }
    public class DriveMemoryModel
    {
        public string FreeSpace { get; set; }
        public string Size { get; set; }
    }
    public class DriveCPUModel
    {
        public string Used { get; set; }
    }
    public class SystemMonitorViewModel : INotifyPropertyChanged
    {
        private readonly SystemMonitorService _service = new();

        private ObservableCollection<DriveDiskModel> _drives = new();
        public ObservableCollection<DriveDiskModel> Drives
        {
            get => _drives;
            set
            {
                _drives = value;
                OnPropertyChanged(nameof(Drives));
            }
        }
        private ObservableCollection<DriveMemoryModel> _drivesMemory = new();
        public ObservableCollection<DriveMemoryModel> DrivesMemory
        {
            get => _drivesMemory;
            set
            {
                _drivesMemory = value;
                OnPropertyChanged(nameof(DrivesMemory));
            }
        }
        private ObservableCollection<DriveCPUModel> _drivesCPU = new();
        public ObservableCollection<DriveCPUModel> DrivesCPU
        {
            get => _drivesCPU;
            set
            {
                _drivesCPU = value;
                OnPropertyChanged(nameof(DrivesCPU));
            }
        }


        public string fetchIn4(string cmd)
        {
            string rawOutput = _service.RunCMD(cmd);
            
            return rawOutput;
        }


        public ObservableCollection<DriveDiskModel> diskInfo(string drawIn4, byte[] Sharekey, string IV)
        {
            var Drives = new ObservableCollection<DriveDiskModel>();
            Drives = ParseOutput(drawIn4,Sharekey, IV);
            return Drives;
        }
        public ObservableCollection<DriveMemoryModel> MemoryInfo(string drawIn4, byte[] Sharekey, string IV)
        {
            var DrivesMemory = new ObservableCollection<DriveMemoryModel>();
            DrivesMemory = ParseOutput_Memory(drawIn4, Sharekey, IV);
            return DrivesMemory;
        }
        public ObservableCollection<DriveCPUModel> CPUInfo(string drawIn4, byte[] Sharekey, string IV)
        {
            var DrivesCPU = new ObservableCollection<DriveCPUModel>();
            DrivesCPU = ParseOutput_CPU(drawIn4, Sharekey, IV);
            return DrivesCPU;
        }

        public string FetchDiskInfo()
        {
            string output = _service.RunCMD("wmic logicaldisk get size,freespace,caption");
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
            return output;
        }
        public string FetchCPUInfo()
        {
            string output = _service.RunCMD("wmic cpu get LoadPercentage");
            return output;// 1 giá trị duy nhất

        }
        

        
        private ObservableCollection<DriveDiskModel> ParseOutput(string output,byte[] Sharekey, string IV)
        {
            var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var result = new ObservableCollection<DriveDiskModel>();

            foreach (var line in lines.Skip(1)) // Skip header
            {
                var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 3)
                {
                    result.Add(new DriveDiskModel
                    {
                        Caption = CryptoService.Encrypt(parts[0], Sharekey, IV),
                        FreeSpace = CryptoService.Encrypt(parts[1], Sharekey, IV),
                        Size = CryptoService.Encrypt(parts[2], Sharekey, IV)
                    });
                }
            }

            return result;
        }
        private ObservableCollection<DriveMemoryModel> ParseOutput_Memory(string output, byte[] Sharekey, string IV)
        {
            var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var result = new ObservableCollection<DriveMemoryModel>();

            foreach (var line in lines.Skip(1)) // Skip header
            {
                var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2)
                {
                    result.Add(new DriveMemoryModel
                    {
                        FreeSpace = CryptoService.Encrypt(parts[0], Sharekey, IV),
                        Size = CryptoService.Encrypt(parts[1], Sharekey, IV)
                    });
                }
            }

            return result;
        }
        private ObservableCollection<DriveCPUModel> ParseOutput_CPU(string output, byte[] Sharekey, string IV)
        {
            var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var result = new ObservableCollection<DriveCPUModel>();

            foreach (var line in lines.Skip(1)) // Skip header
            {
                var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 1)
                {
                    result.Add(new DriveCPUModel
                    {
                        Used = CryptoService.Encrypt(parts[0], Sharekey, IV)
                    });
                }
            }

            return result;
        }
        public string FetchRawInfo(string type)
        {
            string result="";
            if (type == "want_CPUDetail")
            {
                result = _service.RunCMD("wmic cpu get name");
                result += _service.RunCMD("wmic cpu get NumberOfCores,NumberOfLogicalProcessors, MaxClockSpeed,Manufacturer"); // Add CPU load percentage
            }
            else if (type == "want_MemoryDetail")
            {
                result = _service.RunCMD("wmic path win32_VideoController get Name");
                result += _service.RunCMD("wmic path win32_VideoController get AdapterRAM,DriverVersion"); // Add memory details
            }
            else if (type == "want_GPUDetail")
            {
                result = _service.RunCMD("wmic memorychip get BankLabel, Capacity, MemoryType");
                result += _service.RunCMD("wmic memorychip get TypeDetail, Speed, Manufacturer"); // Add GPU details

            }
            else if (type == "want_diskDetail")
            {
                result = _service.RunCMD("wmic logicaldisk get size,freespace,caption");
            }
            else
            {
                result = "Invalid type specified. Please use 'cpu', 'gpu', 'memory', or 'disk'.";
            }
             return result;
        }
        //public string[] FetchAllInfo()
        //{
        //    string CPU = FetchCPUInfo();
        //    string memo = FetchMemoryInfo();
        //    string Disk = FetchDiskInfo();
        //    return new string[] { CPU,  memo, Disk };
        //}

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }


}
