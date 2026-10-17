# Phase 4: inventory vertical slice

## Screens and workflow

The approved Tồn kho menu now opens:

- Phiếu nhập kho / Phiếu xuất kho: `Stock.aspx?screen=stock-receipt` / `stock-issue`.
- Báo cáo hàng nhập / hàng xuất: `Stock.aspx?screen=inbound` / `outbound`.
- Báo cáo hàng tồn > Nhập - Xuất - Tồn: `Stock.aspx?screen=inventory`.

Header and all seven horizontal modules remain in the shared Master Page.
Document lists provide Mới, Sửa, Xóa, Tìm, Xem, In and Làm tươi. In is visibly
disabled until Phase 8. Lists use effective unit/date/number criteria and paging.

Choose Mới, enter header fields and one or more detail lines, then Lưu nháp or
Lưu và ghi sổ. Detail rows include item code, resolved name/unit, quantity, price
and computed amount. Thêm dòng / Bỏ dòng cuối supports up to 100 rows. Completely
empty rows are ignored; partially filled or invalid rows fail server validation.
Tính lại recalculates displayed amounts; persisted amounts are always computed
by SQL. Codes can be selected through the item datalist. The unit is fixed to
the active document-list unit while editing.

Drafts can be edited/deleted using a rowversion token. Posted documents are
immutable here and can only be viewed. There is no silent unposting, cancellation,
stock reversal or editing of purchase/sales-linked warehouse documents. Those
workflows require explicit later implementation. Successful writes redirect to
the list; duplicate document numbers prevent repeated new submissions.

## SQL upgrade

From the repository root:

```powershell
sqlcmd -S '.\SQLEXPRESS' -E -C -b -l 10 -f 65001 -i database/setup.sql
```

The setup preserves existing data/seed and applies `003-inventory.sql`, recording
schema version 2. It creates StockLines (table-valued parameter),
SaveStockDocument, DeleteStockDraft and InventoryReport. Migration DDL is
transactional and rerunnable; version-1 setup now recognizes the supported
version 2 database. No SQL credentials or new libraries are added.

## Posting guarantees

- C# validates number/unit/date, 1-100 lines, known numeric bounds and precision.
  SQL independently validates required header data, lines, references and dates.
- Quantity is positive decimal(18,3), price is nonnegative decimal(19,4).
  Line amounts are SQL-computed decimal(28,4); totals sum actual details.
- Header creation/update, detail replacement, status transition and stock
  validation occur in one transaction with XACT_ABORT and rollback on failure.
- Every mutation first takes an exclusive lock on the canonical DonVi row.
  Writers for the same unit serialize even if callers use different code casing.
  Failed draft posting preserves the original draft and its details.
- For affected items, SQL groups duplicate item lines by document and checks
  every posted running balance ordered by document date then document ID.
  Opening balance is added once. Negative balances at any prefix reject the
  entire transaction, including backdated issues that would break later balances.
- Aggregates are cast to decimal(28,3) before arithmetic to retain all three
  fractional quantity digits; SQL's decimal(38,3) SUM promotion must not reduce
  scale and conceal an issue of 0.001 from zero stock. Out-of-range arithmetic
  fails instead of silently rounding stock checks.
- Dates before the unit's opening date fail. New items without TonDauKy have
  zero opening. Only POSTED warehouse documents affect inventory.
- Application report reads hold a shared unit lock for a consistent result while
  posting requires the exclusive unit lock. No NOLOCK/read-uncommitted queries.

All supported warehouse writes must use these procedures. The local developer
account still has direct database privileges; arbitrary administrator SQL can
bypass application lifecycle rules. Deployment least-privilege roles are not
configured by this phase.

## Reports

Open report → criteria → Tìm/Lọc or Nhận / Xem → server validation → parameterized
stored procedure → grid and totals. There is no hard-coded result data.

Inbound/outbound results include date, document number, item code/name/unit,
quantity, price and amount. N-X-T shows item, opening, inbound, outbound, closing.
Period opening includes the original opening plus all posted movements before
the requested start date. Both date boundaries are inclusive. An optional item
filter is supported; invalid items/ranges fail validation. No movements returns
an empty movement grid; inventory can still show unchanged opening balances.

Refresh retains effective criteria; entered-but-unapplied filters do not silently
change results. Totals cover the whole result, not only its current page. Quantity
totals are explicitly labeled as not converted across units of measure.
Print/PDF/Excel remains Phase 8; the report output button is disabled honestly.

## Acceptance and tests

```powershell
.\tests\run-phase4.ps1
sqlcmd -S '.\SQLEXPRESS' -E -C -b -l 10 -f 65001 -i database/verify-inventory.sql
```

The service suite creates an isolated random test unit/item, checks drafts,
concurrency, rollback, duplicates, invalid inputs, backdating, decimal precision
and independent SQL reconciliation, then removes only its own fixtures.

The hosted Web Forms suite exercises actual server page/postback processing:

1. DVDEMO / HANG001 opening 100.
2. PN001 dated 2026-01-02, +50 at price 100, created and posted through the form.
3. PX001 dated 2026-01-03, -20 at price 100, created and posted through the form.
4. SQL and N-X-T grid both show opening 100, inbound 50, outbound 20, closing 130.
5. Attempt REJECT200 dated 2026-01-04: rejected in the form with no saved header,
   no partial details and unchanged closing 130.
6. Range 2026-01-03 only: opening 150, outbound 20, closing 130.
7. Create/edit/delete a temporary multiline draft through postbacks; posted stock
   remains unchanged. Three report screens and refresh retain effective filters.

PN001/PX001 are retained as synthetic demo facts, not added to seed.sql. Repeated
tests verify existing acceptance documents instead of silently overwriting them.
The baseline verification will intentionally fail if the user later changes the
demo's stock facts. Other test records are removed. The test harness now decodes
ASP.NET response bytes as UTF-8 so Vietnamese messages are checked correctly.

Request-level E2E passes using ApplicationHost; visual browser/keyboard/viewport
verification still awaits IIS/IIS Express tooling. This test host is not a new
application runtime or deployment server. Build and ASPX compilation pass.

## Next phase

Phase 5: sales/purchase subset and related reports. Do not count linked commercial
invoices as an additional stock movement. Phase 4 does not implement those
documents, receivables/payables, financial reporting or output engines.
