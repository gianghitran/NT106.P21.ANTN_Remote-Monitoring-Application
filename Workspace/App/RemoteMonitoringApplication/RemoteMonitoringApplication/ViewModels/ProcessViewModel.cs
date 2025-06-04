using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace RemoteMonitoringApplication.ViewModels
{
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
    public class ProcessViewModel
    {
        
        public void BindProcessListToDataGrid(ProcessList processList, DataGrid targetDataGrid)
        {
            if (processList?.ProcessInfo == null || targetDataGrid == null)
                return;

            // Clear old columns and set new columns
            targetDataGrid.Columns.Clear();

            targetDataGrid.Columns.Add(new DataGridTextColumn { Header = "PID", Binding = new System.Windows.Data.Binding("PID") });
            targetDataGrid.Columns.Add(new DataGridTextColumn{
                                                                Header = "Process Name",
                                                                Binding = new System.Windows.Data.Binding("ProcessName"),
                                                                Width = new DataGridLength(1, DataGridLengthUnitType.Star) 
                                                            });
            targetDataGrid.Columns.Add(new DataGridTextColumn { Header = "CPU", Binding = new System.Windows.Data.Binding("CPU") });
            targetDataGrid.Columns.Add(new DataGridTextColumn { Header = "Memory", Binding = new System.Windows.Data.Binding("Memory") });
            targetDataGrid.Columns.Add(new DataGridTextColumn { Header = "Disk Read", Binding = new System.Windows.Data.Binding("DiskRead") });
            targetDataGrid.Columns.Add(new DataGridTextColumn { Header = "Disk Write", Binding = new System.Windows.Data.Binding("DiskWrite") });

            // Bind data
            targetDataGrid.ItemsSource = new ObservableCollection<ProcessInfo>(processList.ProcessInfo);
        }
    }
}
        
    
