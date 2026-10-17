# AGENTS.md — V3 FINAL
V3 supersedes V1/V2. Do not reopen architecture unless a real technical blocker exists.

## Product
WEB accounting/reporting application inspired by FAST Business Online workflow.
Stack: ASP.NET Framework Web Forms, C#, ASPX, HTML/CSS/JavaScript, SQL Server, T-SQL.
No desktop, React/Vue/Angular, new .NET/.NET Core, AI/ML, sidebar, or standalone Reports top-level menu.

## Layout
Persistent ERP layout: Header -> horizontal main navigation -> workspace.
Top-level modules exactly:
Hệ thống | Tổng hợp | Phải thu | Phải trả | Bán hàng | Mua hàng | Tồn kho

Reports live inside their corresponding business module. Nested dropdown menus are allowed.

## Approved children
Hệ thống: Thông tin chung; Danh mục đơn vị cơ sở.
Tổng hợp: Phiếu kế toán tổng hợp; Báo cáo tài chính > Bảng cân đối kế toán.
Phải thu: Hóa đơn bán hàng; Danh mục khách hàng; Báo cáo công nợ khách hàng.
Phải trả: Hóa đơn mua hàng trong nước; Danh mục nhà cung cấp; Báo cáo công nợ nhà cung cấp.
Bán hàng: Đơn hàng bán; Hóa đơn bán hàng; Báo cáo bán hàng > Chi tiết bán hàng, Doanh thu bán hàng; Báo cáo phân tích > Phân tích doanh thu.
Mua hàng: Đơn hàng mua trong nước; Phiếu nhập mua hàng; Báo cáo mua hàng > Chi tiết nhập/mua hàng; Báo cáo phân tích > Phân tích mua hàng.
Tồn kho: Phiếu nhập kho; Phiếu xuất kho; Danh mục hàng hóa, vật tư; Báo cáo hàng nhập; Báo cáo hàng xuất; Báo cáo hàng tồn > Nhập-Xuất-Tồn.
Do not create fake extra FAST menus.

## Screens
Data/List: context toolbar such as Mới/Thêm, Sửa, Xóa, Tìm, Xem, Import, Export, In, Làm tươi. Only actions relevant to that function.
Entry/Form: create/edit; header-detail for documents where appropriate.
Report: Tìm/Lọc | In/Kết xuất | Làm tươi -> filters -> C# -> parameterized T-SQL -> DB -> result grid -> Print/PDF/Excel.
Làm tươi is mandatory on every data/list/document/report screen.

## Backend
Server validation; parameterized SQL; atomic header/detail transactions; qty > 0; price >= 0; line amount = qty*price; total=sum(details); no negative stock; reports use real DB data, never hard-coded.
Acceptance: HANG001 opening 100, receipt +50, issue -20 => closing 130. Attempt issue 200 afterward => reject atomically.

## Data relationships
Customer -> Sales Invoice -> Sales/Revenue reports -> Receivables.
Supplier -> Purchase document -> Purchase reports -> Payables.
Item -> Warehouse receipts/issues -> Inventory reports.

## Import/output
Master data: Excel import/export.
Reports: Print, PDF, Excel. Output must match current filters.

## UI
Use installed design-taste-frontend only for restrained polish.
DESIGN_VARIANCE ~3/10; MOTION_INTENSITY ~1/10; VISUAL_DENSITY ~7/10.
Keep dense FAST-like ERP interaction. No SaaS dashboard cards, hero UI, glassmorphism, sidebar, giant typography, or unnecessary animation.

## Execution
Read WALKTHROUGH.md and IMPLEMENTATION_PLAN.md. Work phase by phase, build/test honestly, record dependencies and results. Minor implementation details must not reopen architecture planning.
