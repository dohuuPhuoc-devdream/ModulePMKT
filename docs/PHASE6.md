# Phase 6: receivables and payables

## Scope

.NET Framework 4.8 Web Forms, existing native CSS and parameterized ADO.NET/T-SQL. No dependencies installed. The approved seven-module menu tree remains unchanged. The existing receivables/payables destinations now render actual SQL results.

Receivables are posted SALES_INVOICE goods amounts less recorded customer receipts. Payables are posted PURCHASE_INVOICE goods amounts less recorded supplier payments. Aliases use the same invoice/payment facts. Orders and drafts are excluded.

Period calculation for each partner:

- Opening: posted invoice amounts before From minus payments before From.
- Increase: posted invoice amounts dated From through To, inclusive.
- Settlement: payments dated From through To, inclusive.
- Closing: opening + increase - settlement.

There is no imported initial debt balance. Figures cover facts recorded in this application only, and the report states this limitation. Goods amounts retain Phase 5 semantics: no tax, discount, currency conversion or return/credit notes. An invoice fully paid before the period may appear with zero figures. Partners without qualifying invoice history do not appear.

Reports use unit, inclusive date range and optional exact customer/supplier code. A report initially waits for filters. Refresh reloads the effective criteria, even if the user has typed different unapplied filter text. Grid paging is 20 rows; totals include all filtered rows. Print/PDF/Excel remain Phase 8 work.

## Minimal payment workflow

1. Open either sales invoice alias, or open Phải trả / Hóa đơn mua hàng trong nước for a payable invoice. Purchase invoices must reference a posted purchase receipt under Mua hàng.
2. Choose the unit/date range, select an existing posted invoice, then Xem.
3. In Thanh toán hóa đơn, enter a reference, date, positive amount and optional note.
4. Use Ghi nhận thu tiền or Ghi nhận trả tiền. The page redirects after success; reopen Xem to inspect history and remaining balance.
5. Open Báo cáo công nợ khách hàng or Báo cáo công nợ nhà cung cấp and apply the desired period.

This records a settlement against one invoice. It does not execute a bank transfer, create a cash/bank document or post general-ledger entries. A single invoice can have several partial payments. References must be unique within that invoice. Do not reuse a real-world payment with another reference.

Payment dates cannot precede the invoice. Amounts are decimal(28,4), positive and no greater than the remaining invoice amount across all recorded payment dates. Future-dated records are permitted and reduce the invoice's remaining amount available for further allocation; historical reports only include payments up to their end date. The invoice view explicitly labels its payment sum as covering all dates.

A per-unit SQL transaction lock serializes payment recording with commercial posting. An encrypted-viewstate request token plus a unique SQL request ID makes replaying the same submission idempotent. Different contents with an already used token are rejected. Concurrent payments cannot exceed the invoice amount. Payments inherit the unit/partner/type through their invoice FK, avoiding a separate inconsistent partner balance.

Recorded payments are immutable in this subset. Advances, multi-invoice allocation, reversals/corrections, imported opening balances and bank/cash/general-ledger workflows are not implemented. There is no new payment menu and no new company/demo payment seed. Application database privileges remain development-level; direct administrator SQL is outside the workflow protections.

## Files

Created:
- database/005-debt.sql
- src/ReportingModule.Web/DebtStore.cs
- src/ReportingModule.Web/Commerce.Payments.cs
- tests/Phase6Checks.cs, tests/Phase6WebChecks.cs, tests/run-phase6.ps1
- docs/PHASE6.md

Updated:
- database/setup.sql, 001-schema.sql and 003-inventory.sql (include new migration and accept version 4)
- CommerceStore.cs, Commerce.aspx.cs, Navigation.cs and ReportingModule.Web.csproj
- IMPLEMENTATION_PLAN.md, README.md and database/README.md

AGENTS.md and WALKTHROUGH.md are unchanged. Existing CSS is reused with restrained design guidance (3/1/7); no new layout system, cards or motion.

## Reproduce validation

From the workspace root in PowerShell:

```powershell
sqlcmd -S '.\SQLEXPRESS' -E -C -b -l 10 -f 65001 -i database/setup.sql
& .\tests\run-phase6.ps1
sqlcmd -S '.\SQLEXPRESS' -E -C -b -l 10 -f 65001 -i database/verify-inventory.sql
& 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\aspnet_compiler.exe' -v / -p "$PWD\src\ReportingModule.Web" "$PWD\artifacts\phase6-precompiled"
```

Choose a fresh precompile destination on repeat runs. Tests use local Windows SQL authentication; the agent needed approved execution outside its restricted sandbox. No credentials are stored.

Results:
- Release build and ASPX/master precompilation passed without compiler diagnostics.
- Migration applied twice successfully, schema version 4; existing data preserved.
- SQL/service tests passed: fractional balances, opening/closing and inclusive periods, historical exclusion of later payments, both partner types, empty results, order/draft exclusion, invalid dates/amounts/precision/references, cross-unit/type rejection, idempotent replay and changed-payload rejection, full settlement, concurrent overpayment prevention and independent payable reconciliation.
- Hosted Web Forms tests passed: payment controls on posted invoice views, successful partial receipt/payment, history reload, overpayment errors, both debt grids, applied-filter refresh and period opening. Existing order/invoice and five commercial-report request checks also passed.
- Inventory/revenue remain unchanged by settlement; HANG001 still closes at 130. Independent inventory SQL verification and DBCC constraints passed.
- Unique temporary units/items/documents/payments were removed in finally blocks; no persistent company data was invented.

Visual browser/keyboard testing remains pending because IIS/IIS Express/WebApplication targets are unavailable. The request harness exercises ASP.NET rendering and postbacks but is not browser visual QA. No new Phase 6 blocker remains. Phase 7 has not started.
