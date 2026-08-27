<p align="center">
  <video src="cc.mp4" width="100%" controls="controls"></video>
</p>

# BaoTools
<p>
  <img align="right" height="200" src="src/BaoToolsGui/icon.ico" />
  BaoTools là một ứng dụng Desktop trên Windows giúp quản lý và tự động tải game từ Steam một cách tiện lợi và nhanh chóng. App được phát triển dựa trên nền tảng .NET 8 với giao diện WPF hiện đại.
</p>

## Tính năng nổi bật
- **Tự động tải & cài đặt 1 chạm (1-click):** Chỉ cần gõ tên game hoặc nhập AppID, chọn game từ danh sách thả xuống là App sẽ tự động tải và cài đặt ngay lập tức, không cần thao tác thừa.
- **Giới hạn số lượt tải mỗi ngày:** Tích hợp chốt chặn an toàn chỉ cho phép tải tối đa **15 game/ngày** cho mỗi người dùng. Giới hạn sẽ được tự động reset vào ngày hôm sau.
- **Hỗ trợ Online Fix:** Tích hợp sẵn hướng dẫn chi tiết cách cài đặt và sử dụng chế độ chơi Online Fix trực tiếp trong giao diện App.
- **Hiển thị lỗi thông minh:** Tự động phát hiện và cảnh báo khi máy tính hoặc nhà mạng chặn kết nối tới Steam (lỗi SSL/DNS).
- **Hỗ trợ Đa ngôn ngữ:** Tích hợp sẵn gói Tiếng Việt chuẩn chỉ.

## Yêu cầu hệ thống
- Hệ điều hành: Windows 10 hoặc Windows 11.
- Yêu cầu cài đặt: Tải file Portable (không cần cài) hoặc file Setup từ mục Releases. 
- *Lưu ý: Bạn có thể cần dùng Google DNS (8.8.8.8) hoặc GoodbyeDPI/VPN nếu nhà mạng của bạn chặn kết nối tới Steam.*

## Cách cài đặt (Dành cho người dùng)
Bạn vào mục **Releases** ở cột bên phải màn hình GitHub, tải về file:
- BaoTools.exe: Bản Portable chạy ngay không cần cài đặt.
- BaoTools_Setup.exe: Bản cài đặt có tạo icon ngoài Desktop.

## Hướng dẫn Build (Dành cho lập trình viên)
Nếu bạn muốn tự chỉnh sửa mã nguồn và build lại App:
1. Cài đặt [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).
2. Mở file BaoToolsGui.sln bằng Visual Studio 2022 để chỉnh sửa.
3. Để xuất bản Portable, mở Terminal tại thư mục gốc và chạy:
   `ash
   dotnet publish src\BaoToolsGui\BaoToolsGui.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o out_portable
   `

## Bản quyền
Dự án được phân phối dưới giấy phép MIT.
