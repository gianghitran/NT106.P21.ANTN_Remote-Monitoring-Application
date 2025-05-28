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
            var lines = rawOutput.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

            if (lines.Length <= 1)
                return "[No output]";

            var result = new StringBuilder();

            // Lấy danh sách thuộc tính từ lệnh wmic: sau "get"
            string propsPart = cmd.Split(new[] { "get" }, StringSplitOptions.RemoveEmptyEntries)[1];
            string[] headers = propsPart.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                        .Select(h => h.Trim())
                                        .ToArray();

            int columnCount = headers.Length;

            // Tạo danh sách dòng dữ liệu (bỏ dòng header)
            var dataLines = lines.Skip(1).ToList();

            // Tách dữ liệu từng cột theo khoảng cách thực tế (dùng Regex để giữ nguyên giá trị có khoảng trắng)
            var parsedRows = new List<string[]>();

            foreach (var line in dataLines)
            {
                // Cách tốt hơn: tách theo vị trí cố định bằng Regex (giữ nguyên dữ liệu có khoảng trắng)
                var values = Regex.Split(line.Trim(), @"\s{2,}"); // 2 khoảng trắng trở lên
                parsedRows.Add(values);
            }

            // Tính độ rộng cột tối đa
            int[] columnWidths = new int[columnCount];
            for (int i = 0; i < columnCount; i++)
            {
                int maxWidth = headers[i].Length;
                foreach (var row in parsedRows)
                {
                    if (i < row.Length)
                        maxWidth = Math.Max(maxWidth, row[i].Length);
                }
                columnWidths[i] = maxWidth;
            }

            // In dòng header
            for (int i = 0; i < columnCount; i++)
                result.Append(headers[i].PadRight(columnWidths[i] + 2));
            result.AppendLine();

            // In dòng dữ liệu
            foreach (var row in parsedRows)
            {
                for (int i = 0; i < columnCount; i++)
                {
                    string val = (i < row.Length) ? row[i] : "";
                    result.Append(val.PadRight(columnWidths[i] + 2));
                }
                result.AppendLine();
            }

            return result.ToString();
            //return rawOutput;
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
