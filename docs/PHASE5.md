# Phase 5: sales/purchase subset

## Scope and defaults

Uses ASP.NET Framework 4.8 Web Forms and the existing SQL Server database. No packages installed. Native compact ERP controls, seven horizontal modules and the approved menu tree are retained.

| Menu destinations | Shared document type |
| --- | --- |
| Bán hàng / Đơn hàng bán | SALES_ORDER |
| Mua hàng / Đơn hàng mua trong nước | PURCHASE_ORDER |
| Phải thu / Hóa đơn bán hàng; Bán hàng / Hóa đơn bán hàng | SALES_INVOICE |
| Phải trả / Hóa đơn mua hàng trong nước | PURCHASE_INVOICE |
| Mua hàng / Phiếu nhập mua hàng | N warehouse document with a supplier |

These are application codes, not proprietary FAST conventions.

- Orders are independent manually entered documents. Posting an order freezes it but does not reserve stock, change stock, recognize revenue or create debt.
- A posted sales invoice creates exactly one posted warehouse issue in the same transaction. Stock document number is `TM-<commercial ID>`; the filtered unique index prevents duplicate linked movements.
- A posted purchase receipt is a warehouse receipt associated with a supplier; it increases stock once. A posted purchase invoice records the payable and must link to a posted purchase receipt from the same unit and supplier. It does not create another warehouse movement. The receipt and invoice are separate facts, surfaced under their corresponding Mua hàng and Phải trả menus.
- Current linking supports one receipt on each invoice; a receipt can be linked to multiple invoices. Line-level quantity/price reconciliation, an invoice spanning multiple receipts, tax/discount matching, and partial receipt allocation are not implemented.
- Existing purchase invoices that previously created a linked stock receipt are preserved as legacy combined transactions. Migration 008 associates the supplier and receipt reference without replaying stock or changing the prior balances. New entries use the separated workflow.
- Drafts can be edited/deleted with rowversion conflict protection. Posted documents are view-only. Cancellation/reversal and order conversion/fulfillment are not implemented. The existing optional order-source column stays null.
- Quantities have 3 decimal places, prices 4; line amounts round to 4 places and totals are derived. Prices are entered manually. Values are goods amounts before taxes/discounts, with no currency conversion, cost-of-goods or profit calculation.
- Required unit, partner, item, date, number and nonempty details are validated server-side. Posted invoice stock changes reuse the inventory procedure, unit lock and historical nonnegative-stock checks. Orders never bypass invoice stock validation.
- Commercial posting does not generate general-ledger entries. Payments/debt settlement belong to Phase 6; general accounting to Phase 7.

## UI and reports

Open any listed menu, select a unit/date interval, then Mới. Purchase receipts require a supplier and item/quantity lines. On Phải trả / Hóa đơn mua hàng trong nước, choose a supplier and an already posted purchase receipt before posting the invoice. Sales invoices retain their linked stock issue. Use Tính lại, Lưu nháp or Lưu và ghi sổ. The toolbar supports selection, edit/delete drafts, view, search and refresh. Lists page at 20 rows.

Report results load after Tìm/Lọc or Nhận / Xem. Effective filters are unit, inclusive date range and optional customer/supplier code. Refresh retains the applied filters. Only POSTED invoices contribute:

- Chi tiết bán hàng: sales invoice lines.
- Doanh thu bán hàng: goods revenue and distinct invoice count per customer.
- Phân tích doanh thu: quantity and goods revenue per month/item.
- Chi tiết nhập/mua hàng: purchase invoice lines.
- Phân tích mua hàng: quantity and goods value per month/item.

Totals cover every matching row, not only the visible page. Quantity totals across different units of measure are explicitly marked as not converted. Purchase reports query posted purchase invoices; inventory reports query posted warehouse receipts/issues. They do not count an invoice as a second stock movement. Print/PDF/Excel remain disabled until Phase 8.

## Setup and validation

From the repository root in PowerShell:

```powershell
sqlcmd -S '.\SQLEXPRESS' -E -C -b -l 10 -f 65001 -i database/setup.sql
& .\tests\run-phase5.ps1
& .\tests\run-phase4.ps1
sqlcmd -S '.\SQLEXPRESS' -E -C -b -l 10 -f 65001 -i database/verify-inventory.sql
& 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\aspnet_compiler.exe' -v / -p "$PWD\src\ReportingModule.Web" "$PWD\artifacts\phase5-precompiled"
```

Use a fresh precompile destination on repeat runs. Windows authentication must reach the local demo SQL instance; the restricted agent sandbox required approved execution outside it. No credentials are stored.

Results on 2026-09-30:

- Release C# build and ASPX/master precompile passed.
- Migration setup ran twice successfully, schema version 3.
- Service checks passed: orders/drafts excluded; exact invoice totals; date/partner filters; stale write protection; invalid data; duplicate/repost rejection; atomic failed invoice rollback; linked stock count; two simultaneous invoices cannot oversell.
- Actual ASP.NET request checks passed: order create/edit/delete; purchase/sale posting; aliases; posted view; insufficient-stock error; all five filtered report grids and refresh; invalid route 404.
- Inventory regression suite and independent SQL acceptance passed. Existing HANG001 remains 100 + 50 - 20 = 130, with failed issue 200 absent.
- Test data uses unique temporary units and is removed in finally blocks. No new persistent company/demo invoices were invented. Existing PN001/PX001 remain untouched.

The request harness hosts ASP.NET for tests; it is not a production web server or a browser rendering check. No additional implementation blocker was found for Phase 5.

## Purchase workflow correction (2026-10-12)

Migration `database/008-purchase-workflow.sql` adds supplier ownership to purchase receipts and a receipt reference to purchase invoices, then replaces the stock/commercial posting procedures. The `purchase-receipt` destination now opens `Stock.aspx`; `ap-invoice` remains on `Commerce.aspx`. The supplier/unit/posted-receipt relationship is checked inside the same SQL transaction. Only the receipt changes inventory; only the invoice creates payable debt.

Validation: Release build; local setup upgraded to schema version 7 and setup reran successfully; Phase 4, 5, 6 and 8 suites passed; all Phase 3–9 regression suites plus independent SQL checks passed through `tests/run-all.ps1`. Tests cover UI receipt posting/listing, AP invoice linkage, rejection of missing/unknown receipt, no second stock movement, debt/report totals, selected-document outputs, and the unchanged HANG001 130 / issue-200 rejection acceptance. Legacy purchase facts were backfilled by reference only, with no posted effects replayed. No tax, automatic journal posting, cancellation or reversal workflow was added.
