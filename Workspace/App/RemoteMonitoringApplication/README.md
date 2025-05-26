
🧩 Thư viện liên quan

🧠 Lý do sử dụng

🧪 Chi tiết cách hoạt động

### 1. ExecuteCommand(string command)
📚 Thư viện dùng:
```csharp
using System.Diagnostics;
```
💡 Chức năng thư viện:
Cung cấp API để làm việc với tiến trình (Process), dùng để thực thi command trên hệ điều hành.

🧠 Cách code:
```csharp
ProcessStartInfo procStartInfo = new ProcessStartInfo("cmd", "/c " + command);
procStartInfo.RedirectStandardOutput = true;
procStartInfo.UseShellExecute = false;
procStartInfo.CreateNoWindow = true;

Process proc = new() { StartInfo = procStartInfo };
proc.Start();
result = proc.StandardOutput.ReadToEnd();
```

⚙️ Giải thích:
cmd /c chạy một lệnh rồi thoát.

RedirectStandardOutput = true giúp lấy đầu ra dòng lệnh (stdout).

CreateNoWindow = true không hiện cửa sổ console.

UseShellExecute = false để cho phép redirect stream.

### 2. GetFile(string path)
📚 Thư viện dùng:
```csharp
using System.IO;
```
💡 Chức năng thư viện:
Đọc, ghi file (dạng byte), mã hóa base64.

🧠 Cách code:
```csharp
byte[] fileContent = File.ReadAllBytes(path);
string fileContent64 = Encryptor.ConvertStr(fileContent);
File.ReadAllBytes(path) đọc toàn bộ file thành mảng byte[].

Encryptor.ConvertStr(...) chuyển thành base64 (giả định là Base64 encoding).
```

### 3. GetSystemInfo()
📚 Thư viện dùng:
```csharp
using System.Management;
using Microsoft.Win32;
using System.Net;
```
💡 Thư viện quan trọng:
System.Management: dùng WMI để truy xuất info OS, RAM, Disk.

Microsoft.Win32: đọc registry.

System.Net: lấy IP.

🧠 Ví dụ cụ thể:
Lấy tên máy từ Registry:

```csharp
string path = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\ComputerName\ActiveComputerName";
string name = Registry.GetValue(path, "ComputerName", "").ToString();
//Lấy IP nội bộ:
string hostName = Dns.GetHostName();
IPAddress[] addr = Dns.GetHostEntry(hostName).AddressList;
//Lấy RAM bằng WMI:
ManagementObjectSearcher searcher = new("SELECT TotalVisibleMemorySize, FreePhysicalMemory FROM Win32_OperatingSystem");
```
### 4. GetProcDump(string pid)
📚 Thư viện thường dùng:
Giả định MiniDumpHandler là module tự định nghĩa hoặc wrapper cho DbgHelp.dll.

Cần Windows API như: MiniDumpWriteDump() từ DbgHelp.dll.

🧠 Cách hoạt động:
Mở handle tiến trình (OpenProcess).

Tạo file dump (MiniDumpWriteDump(handle, ...)) ghi ra file .dmp.

### 5. GetScreenShot()
📚 Thư viện dùng:
```csharp
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
```
💡 Mục tiêu:
Chụp màn hình bằng WinAPI và GDI+.

🧠 Code quan trọng:
```csharp
Rectangle bounds = new Rectangle(left, top, width, height);
Bitmap result = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);

using (Graphics graphics = Graphics.FromImage(result))
{
    graphics.CopyFromScreen(new Point(bounds.Left, bounds.Top), Point.Empty, bounds.Size);
}
Graphics.CopyFromScreen → chụp toàn bộ màn hình theo kích thước virtual screen.
```
Bitmap → tạo ảnh để lưu về MemoryStream.

### 6. GetProcList()
📚 Thư viện:
```csharp
using System.Diagnostics;
```
🧠 Cách code:
```csharp
Process[] processes = Process.GetProcesses();
foreach (Process process in processes)
{
    string info = $"{process.ProcessName} -- {process.Id} -- {process.Threads.Count} -- {process.WorkingSet64}";
}
Lấy danh sách tất cả tiến trình.

WorkingSet64: lượng RAM tiến trình sử dụng.

Threads.Count: số thread trong tiến trình.
```
### 7. Stealer.GetCredentials()
📚 Không có sẵn trong .NET
Có thể là custom module dùng Windows API, hoặc can thiệp trình duyệt.

🧠 Cách thường dùng (nếu reverse malware):
Trích xuất từ Chrome Login Data SQLite file.

Dùng CryptUnprotectData (WinAPI) để giải mã mật khẩu.

### 8. CaptureDesktop() / CaptureWindow()...
📚 Thư viện:
```csharp
using System.Runtime.InteropServices;
```
🧠 WinAPI liên quan:
GetSystemMetrics, GetWindowRect, GetForegroundWindow: lấy kích thước màn hình/tiến trình đang focus.
```csharp
Graphics.CopyFromScreen: chụp màn hình.
```
Ví dụ:
```csharp
[DllImport("user32.dll")]
private static extern IntPtr GetForegroundWindow();
```
Dùng DllImport để gọi hàm gốc từ user32.dll.

### 9. ReceiveFile(...)
📚 Thư viện:
```csharp
using System.Collections.Concurrent;
using System.IO;
```
🧠 Ý nghĩa:
Dùng ConcurrentDictionary để gom các chunk file theo chỉ số.

Gộp và ghi thành file mới:

```csharp
string fileBase64 = string.Join("", chunks.OrderBy(kv => kv.Key).Select(kv => kv.Value));
byte[] fileData = Encryptor.InvertStr(fileBase64);
File.WriteAllBytes(filePath, fileData);
```