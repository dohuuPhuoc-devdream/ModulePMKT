# Reporting Module — V3

Ứng dụng thực hành kế toán/báo cáo cho kỳ thực tập FAST, dùng ASP.NET Framework 4.8 Web Forms, C#/ASPX, HTML/CSS/JavaScript thuần và SQL Server/T-SQL.

## Chức năng hiện có

- Bảy phân hệ đúng V3; menu ngang luôn hiện, menu con lồng nhau, thanh công cụ theo chức năng và Làm tươi.
- Bốn danh mục: đơn vị, khách hàng, nhà cung cấp, hàng hóa; CRUD, tìm kiếm, Excel import/export.
- Đơn vị có trường thông tin pháp lý để người dùng tự khai báo; mỗi đơn vị có kho mặc định `KHO01`, người dùng có đơn vị làm việc mặc định.
- Phiếu nhập mua cập nhật kho; hóa đơn mua liên kết phiếu nhập và cập nhật công nợ riêng. Hóa đơn bán tạo phiếu xuất liên kết. Đơn hàng, bản nháp, ghi sổ nguyên tử, kiểm tra tồn kho và xung đột sửa đổi. Tồn kho hiện theo một kho mặc định `KHO01` cho mỗi đơn vị; chưa hỗ trợ điều chuyển hay tồn nhiều kho.
- Thanh toán từng hóa đơn; báo cáo công nợ khách hàng/nhà cung cấp.
- Phiếu kế toán thủ công và bảng cân đối đại diện với hệ tài khoản thực hành.
- 11 báo cáo truy vấn SQL thực; Print/PDF/Excel từ toàn bộ kết quả theo điều kiện đã áp dụng.
- Phiếu nhập mua dùng chung model cho HTML/PDF/Excel; PDF vector có thể chọn/tìm kiếm, phông Times New Roman và đủ vai trò ký/dòng ngày theo mẫu. Excel có công thức tổng tiền, kho, người giao hàng, thông tin đối tác và số tiền bằng chữ.

## Chuẩn bị và chạy

Cần Windows, .NET Framework 4.8 Developer Pack, MSBuild, SQL Server và sqlcmd. Để chạy giao diện trực tiếp, cần Visual Studio có công cụ ASP.NET Web Forms cùng IIS Express, hoặc IIS cấu hình ASP.NET Framework 4.8.

Máy hiện tại đã có IIS Express và WebApplication targets sau khi bổ sung công cụ Visual Studio. Phase 10 thêm đăng nhập, phân quyền, quản trị và nhật ký. Giao diện đã được kiểm tra giới hạn trên localhost; các luồng trình duyệt cần đăng nhập và nghiệm thu toàn bộ vẫn cần thực hiện.

Từ thư mục gốc:

```powershell
.\tools\restore-packages.ps1
sqlcmd -S '.\SQLEXPRESS' -E -C -b -l 10 -f 65001 -i database/setup.sql
& 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe' ReportingModule.sln /t:Build /p:Configuration=Release /nologo /verbosity:minimal
```

`packages/` bị loại khỏi git. Trên bản clone mới, `restore-packages.ps1` dùng NuGet
CLI để khôi phục đúng các phiên bản trong `src/ReportingModule.Web/packages.config`,
kiểm tra DLL/targets mà project tham chiếu, rồi build Release. Nếu máy chưa có NuGet
CLI, script tải bản chính thức vào thư mục tạm và kiểm tra chữ ký Microsoft trước khi
chạy. Có thể truyền `-NuGetPath 'C:\path\nuget.exe'`; dùng `-SkipBuild` nếu chỉ muốn
khôi phục và kiểm tra gói. Cần kết nối NuGet.org ở lần khôi phục đầu tiên.

Khi nâng/hạ phiên bản NuGet, cập nhật `packages.config`, các `HintPath` và `Import`
trong `.csproj`, cùng binding redirects trong `Web.config` nếu assembly yêu cầu.
Sau đó chạy lại script restore để xác nhận các tham chiếu và bản Release đều hợp lệ.

Setup tạo database demo `ReportingModuleDemo`, áp dụng migration đến version 15 và seed dữ liệu. Chạy lại giữ dữ liệu; từ chối database có sẵn không mang dấu demo của dự án. Không dùng dữ liệu công ty thật.

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

Chạy bằng tài khoản Windows truy cập được SQL demo. `run-all.ps1` chạy Phase 3–9, Phase 11 và kiểm tra SQL/PDF/Excel độc lập; khóa chạy song song, bật tài khoản test một lần và tắt tài khoản sau suite. Log nằm ở `artifacts/quality`. Mỗi phase tạo và dọn fixture riêng. Phase 4 giữ PN001/PX001 cho nghiệm thu HANG001: 100 + 50 - 20 = 130; xuất 200 bị từ chối nguyên tử. Không chạy test này trên database nghiệp vụ thật.

`tests/verify-phase8.py` kiểm tra độc lập file kết xuất, cần openpyxl, pypdf, pypdfium2 và Pillow; các thư viện QA có sẵn trong runtime Codex, không phải dependency của ứng dụng. PDF được tạo bằng QuestPDF 2026.9.1 (Community license cho mục đích học tập), dùng Times New Roman với các kiểu regular/bold/italic/bold-italic đã cài; nếu phông Times không có, dùng Georgia. Ứng dụng không phân phối các phông Microsoft. Chứng từ nhập mua và các dòng hàng mua/bán, nhập/xuất kho lưu thành tiền đến đồng: từng dòng được làm tròn nửa ra xa 0, rồi tổng cộng các dòng đã làm tròn. Các package runtime và binding redirects nằm trong `packages.config`/`Web.config`. Checklist xem PDF, hộp thoại in và in thử nằm trong [Phase 11](docs/PHASE11.md); kiểm tra PDF tự động không thay thế việc in thật trên máy.

## Phạm vi và giới hạn

- Đây là demo cục bộ có đăng nhập/phân quyền. Chưa hoàn tất nghiệm thu trình duyệt có tài khoản thật, backup/restore, cấu hình IIS production hoặc public deployment. Giữ cổng SQL riêng tư.
- Hóa đơn tính tiền hàng, chưa có thuế/chiết khấu/đa tiền tệ. Đơn hàng chưa chuyển tự động sang hóa đơn.
- Báo cáo kho thể hiện số lượng ở kho mặc định `KHO01`; chưa có tồn đầu kỳ theo giá trị, phương pháp tính giá xuất hoặc giá vốn. Không dùng các số này như báo cáo định giá hàng tồn kho.
- Liên kết hiện tại cho phép một phiếu nhập trên mỗi hóa đơn mua; khi ghi sổ, từng dòng phải khớp mã hàng, số lượng và đơn giá với phiếu nhập. Chưa hỗ trợ thuế/chiết khấu, chênh lệch giá hoặc phân bổ một hóa đơn cho nhiều phiếu nhập.
- Công nợ dựa trên hóa đơn và thanh toán đã ghi nhận; chưa có số dư công nợ đầu kỳ nhập ngoài, ứng trước hoặc đảo thanh toán.
- Sổ tổng hợp nhập thủ công, chưa tự hạch toán từ kho/mua/bán/thanh toán. Bảng cân đối và tài khoản chỉ mang tính thực hành.
- PDF báo cáo và chứng từ là văn bản vector có thể tìm/chọn. Kết xuất tối đa 5.000 dòng; quá giới hạn báo lỗi, không cắt dữ liệu âm thầm.
- Danh mục TT99 có 184 tài khoản đã được đối chiếu mã và tên với Phụ lục II, mục A của Công báo và nạp bằng migration 014. `TinhChat` dư Nợ/Có/lưỡng tính để rỗng vì phần danh mục không công bố thuộc tính đó; hệ thống chưa dùng thuộc tính này để chặn ghi sổ hoặc quyết định cột số dư.
- Việc nạp danh mục tài khoản TT99 chỉ xác nhận mã, tên và cấu trúc danh mục. Đây không phải xác nhận phần mềm tuân thủ đầy đủ chế độ kế toán hay đủ điều kiện dùng cho sổ kế toán doanh nghiệp. Bộ Tài chính nêu Thông tư 99/2025/TT-BTC có hiệu lực từ 01/01/2026 và áp dụng cho năm tài chính bắt đầu từ hoặc sau ngày đó; báo cáo Final cần xác định đối tượng áp dụng và quy định hiện hành theo phạm vi đề tài. ([Bộ Tài chính – thông tin triển khai Thông tư 99/2025/TT-BTC](https://www.mof.gov.vn/tin-tuc-tai-chinh/tin-chinh-sach-tai-chinh/tap-huan-trien-khai-thong-tu-so-992025tt-btc))
- Dữ liệu mẫu dùng tên đơn vị, khách hàng, nhà cung cấp, mặt hàng và tài khoản thực hành dễ đọc. Seed `database/002-seed.sql` áp dụng cho database mới; migration `database/012-demo-display-names.sql` đổi nhãn placeholder cũ và chỉ điền tên pháp lý/địa chỉ DVDEMO khi các trường còn trống hoặc chứa placeholder. Thông tin PicoCorp là dữ liệu mẫu chưa được xác minh; mã số thuế và chi tiết liên hệ vẫn để trống.
- Snapshot kết xuất thuộc phiên, hết hạn sau 20 phút, tối đa 8 snapshot. Sửa ô lọc mà chưa áp dụng không đổi dữ liệu kết xuất.

## Tài liệu

- [Hướng dẫn demo và nghiệm thu](docs/DEMO.md)
- [Kết quả chất lượng Phase 9](docs/PHASE9.md)
- [Kết quả Phase 10](docs/PHASE10.md)
- [Kế hoạch và lịch sử triển khai](IMPLEMENTATION_PLAN.md)
- [Database](database/README.md)
- Chi tiết từng phần: [danh mục](docs/PHASE3.md), [kho](docs/PHASE4.md), [mua/bán](docs/PHASE5.md), [công nợ](docs/PHASE6.md), [tổng hợp](docs/PHASE7.md), [kết xuất](docs/PHASE8.md).

