using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SERVER_RemoteMonitoring.ViewModels
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
    public class RemoteInfoMessage
    {
        public List<DriveDiskModel> Drives { get; set; }
        public List<DriveMemoryModel> Memory { get; set; }
        public List<DriveCPUModel> CPU { get; set; }

    }

    public class BaseResponse_RemoteInfo<T>
    {
        public string status { get; set; }
        public string command { get; set; }
        public RemoteInfoMessage message { get; set; }
    }
    public class ProcessInfo
    {
        public int PID { get; set; }
        public string ProcessName { get; set; }
        public string CPU { get; set; }
        public string Memory { get; set; }
        public string DiskRead { get; set; }
        public string DiskWrite { get; set; }

    }
    public class ProcessList
    {
        public string RealTime { get; set; }
        public List<ProcessInfo> ProcessInfo { get; set; }

    }
    class PerformaceViewModel
    {
    }
}
