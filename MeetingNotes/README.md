# Tiêu chí
| *Tiêu chí*          | *Cách thực hiện trong C#* |
|----------------------|--------------------------|
| *App Logic + Socket Logic* (5 điểm) | Dùng *TCP Socket* hoặc *WebSockets* để giao tiếp giữa Client & Server. |
| *I/O (File, Network …)* (0.5 điểm) | Ghi file log bằng StreamWriter trong C#.  <br> Gửi dữ liệu qua mạng bằng TCPClient. |
| *Database* (0.5 điểm) | Lưu trữ dữ liệu trong *SQLite*. lưu dashboard|
| *Thread* (0.5 điểm) | Dùng Thread hoặc Task.Run() để chạy các tiến trình nền như theo dõi phím bấm, màn hình. |
| *Sign up/Sign in* (0.5 điểm) | Tạo form đăng nhập với *WPF*.  <br> Lưu user vào Database. |
| *Multi Client* (0.5 điểm) | Nhiều Client gửi dữ liệu về Server bằng *TCPSockets* . |
| *Multi Server* (0.5 điểm) | Chia nhiều server để xử lý dữ liệu từ Client, có thể dùng *Load Balancing*. |
| *Cryptography* (0.5 điểm) | Mã hóa dữ liệu. |
| *Demo via LAN* (0.5 điểm) | Cho phép Client kết nối Server qua *Local Network* (IP nội bộ). |
| *Demo via Internet* (0.5 điểm) | (ngrox)/localtonet. |
| *Load Balancing* (1 điểm) | nginx - Local: tạo node giữa client và server. |


# Roadmap:

### Giai đoạn 1: Nghiên cứu & Lên kế hoạch 
| Công việc | Mô tả |
|-----------|-------|
| Xác định yêu cầu hệ thống | Client & Server hoạt động thế nào? |
| Chọn công nghệ | UI: WPF, Server: ASP.NET Core, Database: SQLite |
| Thiết kế kiến trúc | Vẽ sơ đồ luồng dữ liệu |

### Giai đoạn 2: Xây dựng Client cơ bản 
| Công việc | Mô tả |
|-----------|-------|
| Xây dựng UI bằng WPF | Thiết kế giao diện đơn giản |
| Ghi màn hình | Graphics.CopyFromScreen để chụp màn hình |
| Ghi bàn phím & chuột | Dùng Global Hook để theo dõi nhập liệu |
| Gửi dữ liệu lên Server | Sử dụng WebSockets hoặc HTTP API |

## Giai đoạn 3: Xây dựng Server cơ bản 
| Công việc | Mô tả |
|-----------|-------|
| Tạo API nhận dữ liệu | video,log,process list, process dump, info systems, cpu, ram,... |
| Lưu trữ dữ liệu | Lưu ảnh & log vào SQLite |
| Test Server | Kiểm thử với nhiều Client |

### Giai đoạn 4: Xây dựng Dashboard giám sát 
| Công việc | Mô tả |
|-----------|-------|
| Giao diện Dashboard | Hiển thị danh sách Client |
| Xem màn hình Client | Load video từ Server |
| Xem lịch sử nhập liệu | Hiển thị log, info, process,... |

### Giai đoạn 5: Cải thiện hiệu suất & bảo mật 
| Công việc | Mô tả |
|-----------|-------|
| Mã hóa dữ liệu | AES-256 hoặc RSA |
| Cải thiện tốc độ | Giảm dung lượng ảnh trước khi gửi |
| Đăng nhập bảo mật | Xác thực người dùng khi giám sát |

### Giai đoạn 6: Kiểm thử & Triển khai 
| Công việc | Mô tả |
|-----------|-------|
| Test nhiều Client | Đảm bảo Server hoạt động ổn định |
| Tối ưu UI/UX | Cải thiện trải nghiệm người dùng |
| Internet | Triển khai |

## Tổng kết Roadmap
| Giai đoạn | Công việc | Thời gian dự kiến |
|-----------|----------|------------------|
| *1* | Nghiên cứu & Lên kế hoạch | 1-2 ngày |
| *2* | Xây dựng Client cơ bản |  |
| *3* | Xây dựng Server cơ bản |  |
| *4* | Xây dựng Dashboard |  |
| *5* | Cải thiện bảo mật & hiệu suất |  |
| *6* | Kiểm thử & triển khai |  |

*Tổng thời gian*: ---




## Phân công công việc
- Frontend: WPF (.NET)
- C#

  
|           | Công việc| Nhân sự          |
|-----------|----------|------------------|
| Fontend | Login | Trần Trọng Nghĩa |
| Fontend | Client | Nguyễn Đa Vít |
| Fontend | Server | Trần Gia Nghi |
| Backend | Các thông điệp từ máy bị theo dõi | Trần Gia Nghi |
| Backend | Giao tiếp Client | Nguyễn Đa Vít |
| Backend | Giao tiếp server + phân quyền + Login | Trần Trọng Nghĩa |
| Backend | Database - sqllite | Trần Gia Nghi |
| Cryptography | RSA + AES | Nguyễn Đa Vít |
| Mở rộng | Load Balancing |  |
| Mở rộng | Demo via Internet : Ngrox/localtonet | |




