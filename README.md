# Reporting Module — V3

Ứng dụng thực hành kế toán/báo cáo cho kỳ thực tập FAST, dùng ASP.NET Framework 4.8 Web Forms, C#/ASPX, HTML/CSS/JavaScript thuần và SQL Server/T-SQL.

## Chức năng hiện có

- Bảy phân hệ đúng V3; menu ngang luôn hiện, menu con lồng nhau, thanh công cụ theo chức năng và Làm tươi.
- Bốn danh mục: đơn vị, khách hàng, nhà cung cấp, hàng hóa; CRUD, tìm kiếm, Excel import/export.
- Đơn vị có trường thông tin pháp lý để người dùng tự khai báo; mỗi đơn vị có kho mặc định `KHO01`, người dùng có đơn vị làm việc mặc định.
- Phiếu nhập mua cập nhật kho; hóa đơn mua liên kết phiếu nhập và cập nhật công nợ riêng. Hóa đơn bán tạo phiếu xuất liên kết. Đơn hàng, bản nháp, ghi sổ nguyên tử, kiểm tra tồn kho và xung đột sửa đổi.
- Thanh toán từng hóa đơn; báo cáo công nợ khách hàng/nhà cung cấp.
- Phiếu kế toán thủ công và bảng cân đối đại diện với hệ tài khoản thực hành.
- 11 báo cáo truy vấn SQL thực; Print/PDF/Excel từ toàn bộ kết quả theo điều kiện đã áp dụng.
- Phiếu nhập mua có model chứng từ dùng chung cho HTML/PDF/Excel và mẫu Excel có công thức tổng tiền, kho, người giao hàng, thông tin đối tác và số tiền bằng chữ. PDF hiện vẫn là ảnh, chưa tìm kiếm/chọn được văn bản.

## Chuẩn bị và chạy

Cần Windows, .NET Framework 4.8 Developer Pack, MSBuild, SQL Server và sqlcmd. Để chạy giao diện trực tiếp, cần Visual Studio có công cụ ASP.NET Web Forms cùng IIS Express, hoặc IIS cấu hình ASP.NET Framework 4.8.

Máy hiện tại đã có IIS Express và WebApplication targets sau khi bổ sung công cụ Visual Studio. Phase 10 thêm đăng nhập, phân quyền, quản trị và nhật ký. Giao diện đã được kiểm tra giới hạn trên localhost; các luồng trình duyệt cần đăng nhập và nghiệm thu toàn bộ vẫn cần thực hiện.

Từ thư mục gốc:

```powershell
sqlcmd -S '.\SQLEXPRESS' -E -C -b -l 10 -f 65001 -i database/setup.sql
& 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe' ReportingModule.sln /t:Build /p:Configuration=Release /nologo /verbosity:minimal
```

Setup tạo database demo `ReportingModuleDemo`, áp dụng migration đến version 10 và seed dữ liệu. Chạy lại giữ dữ liệu; từ chối database có sẵn không mang dấu demo của dự án. Không dùng dữ liệu công ty thật.

Sau khi chạy setup, tạo Administrator đầu tiên tại máy bằng script dưới đây. Script yêu cầu mật khẩu trực tiếp trong PowerShell dưới dạng secure string và không lưu nó vào dự án:

```powershell
.\tools\provision-admin.ps1
```

Script dùng Windows Integrated Security. Không chạy provisioning bằng mật khẩu thật ở máy dùng chung.

Connection string nằm trong `src/ReportingModule.Web/Web.config`: mặc định `.\SQLEXPRESS`, Windows authentication. Tài khoản chạy ứng dụng phải được cấp quyền database phù hợp. `TrustServerCertificate=True` và `sqlcmd -C` phục vụ SQL cục bộ.

Khi có web tooling, mở `ReportingModule.sln`, chọn project web và `Default.aspx` làm trang bắt đầu, chạy bằng IIS Express. Không phục vụ ASPX bằng một HTTP server tĩnh.

Precompile vào thư mục đầu ra chưa tồn tại:

```powershell
& 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\aspnet_compiler.exe' -v / -p "$PWD\src\ReportingModule.Web" "$PWD\artifacts\manual-precompiled"
```

## Chạy nhanh trên máy này

Trong PowerShell tại thư mục dự án, chạy `.\tools\run-local.ps1` rồi mở http://localhost:8080/Default.aspx. Giữ terminal đang chạy; nhấn Q để dừng IIS Express. Nếu cổng 8080 đang dùng, chọn `.\tools\run-local.ps1 -Port 8081`.

## Kiểm thử

```powershell
.\tests\run-all.ps1
.\tests\run-phase10.ps1
```

Chạy bằng tài khoản Windows truy cập được SQL demo. `run-all.ps1` chạy Phase 3–9 và kiểm tra SQL độc lập; `run-phase10.ps1` chạy Release build, ASP.NET precompile và security tests. Log Phase 3–9 nằm ở `artifacts/quality`. Test tạo fixture riêng. Phase 4 giữ PN001/PX001 cho nghiệm thu HANG001: 100 + 50 - 20 = 130; xuất 200 bị từ chối nguyên tử. Không chạy test này trên database nghiệp vụ thật.

`tests/verify-phase8.py` là kiểm tra độc lập tùy chọn cho file kết xuất, cần openpyxl, pypdf, pypdfium2 và Pillow; các thư viện QA có sẵn trong runtime Codex, không phải dependency của ứng dụng. Ứng dụng không có package bên thứ ba mới.

## Phạm vi và giới hạn

- Đây là demo cục bộ có đăng nhập/phân quyền. Chưa hoàn tất nghiệm thu trình duyệt có tài khoản thật, backup/restore, cấu hình IIS production hoặc public deployment. Giữ cổng SQL riêng tư.
- Hóa đơn tính tiền hàng, chưa có thuế/chiết khấu/đa tiền tệ. Đơn hàng chưa chuyển tự động sang hóa đơn.
- Liên kết hiện tại cho phép một phiếu nhập trên mỗi hóa đơn mua; chưa đối chiếu số lượng/giá từng dòng, chưa phân bổ một hóa đơn cho nhiều phiếu nhập.
- Công nợ dựa trên hóa đơn và thanh toán đã ghi nhận; chưa có số dư công nợ đầu kỳ nhập ngoài, ứng trước hoặc đảo thanh toán.
- Sổ tổng hợp nhập thủ công, chưa tự hạch toán từ kho/mua/bán/thanh toán. Bảng cân đối và tài khoản chỉ mang tính thực hành.
- PDF là ảnh có phân trang, không tìm kiếm/chọn văn bản. Kết xuất tối đa 5.000 dòng và PDF tối đa 200 trang; quá giới hạn báo lỗi, không cắt dữ liệu âm thầm.
- Snapshot kết xuất thuộc phiên, hết hạn sau 20 phút, tối đa 8 snapshot. Sửa ô lọc mà chưa áp dụng không đổi dữ liệu kết xuất.

## Tài liệu

- [Hướng dẫn demo và nghiệm thu](docs/DEMO.md)
- [Kết quả chất lượng Phase 9](docs/PHASE9.md)
- [Kết quả Phase 10](docs/PHASE10.md)
- [Kế hoạch và lịch sử triển khai](IMPLEMENTATION_PLAN.md)
- [Database](database/README.md)
- Chi tiết từng phần: [danh mục](docs/PHASE3.md), [kho](docs/PHASE4.md), [mua/bán](docs/PHASE5.md), [công nợ](docs/PHASE6.md), [tổng hợp](docs/PHASE7.md), [kết xuất](docs/PHASE8.md).

