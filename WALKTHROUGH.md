# WALKTHROUGH.md — V3 FINAL

## Architecture
Browser -> ASP.NET Framework Web Forms (ASPX/C# + HTML/CSS/JS) -> business logic -> T-SQL -> SQL Server.

Persistent layout:
Header
Horizontal menu: Hệ thống | Tổng hợp | Phải thu | Phải trả | Bán hàng | Mua hàng | Tồn kho
Workspace

No sidebar. No top-level Báo cáo. Reports belong to modules.

## Menu tree
Hệ thống
- Thông tin chung
- Danh mục đơn vị cơ sở

Tổng hợp
- Phiếu kế toán tổng hợp
- Báo cáo tài chính
  - Bảng cân đối kế toán

Phải thu
- Hóa đơn bán hàng
- Danh mục khách hàng
- Báo cáo công nợ khách hàng

Phải trả
- Hóa đơn mua hàng trong nước
- Danh mục nhà cung cấp
- Báo cáo công nợ nhà cung cấp

Bán hàng
- Đơn hàng bán
- Hóa đơn bán hàng
- Báo cáo bán hàng
  - Chi tiết bán hàng
  - Doanh thu bán hàng
- Báo cáo phân tích
  - Phân tích doanh thu

Mua hàng
- Đơn hàng mua trong nước
- Phiếu nhập mua hàng
- Báo cáo mua hàng
  - Chi tiết nhập/mua hàng
- Báo cáo phân tích
  - Phân tích mua hàng

Tồn kho
- Phiếu nhập kho
- Phiếu xuất kho
- Danh mục hàng hóa, vật tư
- Báo cáo hàng nhập
- Báo cáo hàng xuất
- Báo cáo hàng tồn
  - Nhập - Xuất - Tồn

## UI patterns
Data/List example:
DANH MỤC KHÁCH HÀNG
Mới | Sửa | Xóa | Tìm | Import | Export | Làm tươi
Grid below.

Document list can instead use Mới | Sửa | Xóa | Tìm | Xem | In | Làm tươi.
Toolbars are context-sensitive.

Report example:
BÁO CÁO NHẬP-XUẤT-TỒN
Tìm/Lọc | In/Kết xuất | Làm tươi
Mã hàng | Tên hàng | Tồn đầu | Nhập | Xuất | Tồn cuối

Tìm/Lọc opens criteria such as date range, warehouse/item/customer/supplier as appropriate.
Criteria -> server validation -> parameterized T-SQL -> result grid -> Print/PDF/Excel.

Operational reports use normal grids. Bảng cân đối kế toán may use hierarchical/tree rows.
Text left; useful codes center; numbers/money right; clear totals; pagination; sticky headers when useful.

## Core data
DonVi, KhachHang, NhaCungCap, HangHoa; sales/purchase/warehouse document headers + details; minimal general-accounting data required for the representative financial report.
Normalize and reuse shared facts rather than duplicating inconsistent data.

## Rules
Required fields server validated. Unique codes where required. Qty > 0. Price >= 0. Amount=qty*price. Total=sum details. Header/detail transaction. FK integrity. Stock issue checks available stock. Parameterized SQL.

## Demo data
HANG001 opening=100; PN001 +50; PX001 -20; inventory report closing=130. Issue 200 must fail without partial writes. Seed data is for reproducible demo; manual UI entry remains functional.

## Import/export
Master data supports Excel import/export. Validate file structure/rows and report errors. Never silently overwrite duplicates.
Reports support Print/PDF/Excel and use current effective filters.

## Implementation order
1. Web Forms solution + shared ERP shell.
2. SQL schema + seed.
3. Master data.
4. Inventory vertical slice + inventory reports.
5. Sales/purchase subset + reports.
6. Receivable/payable reports.
7. General/financial representative report.
8. Excel/PDF/print.
9. E2E tests, Taste polish, README/demo.

## Done
Web Forms runs; 7 modules; approved children only; horizontal navigation; contextual toolbars; refresh everywhere; CRUD/master import-export; real document DB writes; transaction/validation; no negative inventory; real filtered reports; Print/PDF/Excel; acceptance 130/reject-200; dense polished ERP UI.
