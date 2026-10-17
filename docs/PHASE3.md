# Phase 3: master data

## Screens

The existing module menus now open `Catalog.aspx?screen=items`, `customers`,
`suppliers` or `units`. Each uses the shared ERP shell and provides Mới, Sửa,
Xóa, Tìm, Import, Export and Làm tươi. Other functions remain placeholders.

- Search matches literal text in code/name. `%`, `_`, `[` and SQL-like input do
  not become wildcard/query syntax. Refresh retains the effective filter.
- Grid pages contain 20 records. Select a row before editing/deleting it.
- Required fields, lengths, codes, price precision and dates are checked on the
  server. Codes permit ASCII letters, digits, hyphen and underscore and cannot
  change on edit. Prices are nonnegative decimal(19,4), entered with a dot.
- Updates/deletes compare the original field snapshot against the DB and reject
  stale edits. FK-referenced records cannot be deleted. Changing a unit's opening
  date is blocked when it already has opening balances or documents.
- Mutation requests use POST, validated Web Forms events and encrypted/MACed
  viewstate bound to the session and catalog. Successful mutations redirect to
  a GET, avoiding accidental resubmission on refresh. Values in grids/messages
  are HTML-encoded; error responses do not expose SQL connection details.

## Excel contract

Use **Import → Tải file mẫu** for exact column names/order. Files are genuine
`.xlsx` packages, not CSV renamed as Excel. Export includes all rows matching the
current effective filter, including rows beyond the current grid page.

| Catalog | Stable columns |
|---|---|
| Items | MaHang, TenHang, DonViTinh, GiaNhap, GiaBan |
| Customers | MaKH, TenKH, MaSoThue, DiaChi, DienThoai |
| Suppliers | MaNCC, TenNCC, MaSoThue, DiaChi, DienThoai |
| Units | MaDonVi, TenDonVi, NgayBatDau |

Native .NET Framework ZIP/XML APIs are used. No Excel application, COM automation,
NuGet package or frontend dependency was installed.

- One worksheet, maximum 5 MB compressed, 20 MB expanded, 100 package entries,
  and 5000 data rows. Export also caps at 5000 rows; narrow the filter if needed.
- Exact header order, consecutive row numbers beginning at 1, no extra columns,
  merged cells, formulas, macros or 1904 date system. Remove unused/blank rows.
  Legacy `.xls` is unsupported. XML DTD/external entity resolution is disabled.
- Inline/shared strings and ordinary numeric cells are supported. Unit dates
  accept `yyyy-MM-dd` text or Excel numeric dates (1900 system, no time portion).
  Dates must be at least 1900-01-01. Codes should be text to retain leading zeros.
- Prices export as numeric cells, dates as styled numeric date cells, and text
  as strings. Formula-like text stays text. Excel itself has a 15-significant-digit
  numeric precision limit; avoid using Excel to round-trip amounts beyond that
  precision, even though SQL decimal supports more.
- Import validates all rows first, reports up to 20 row errors, then inserts in
  one transaction. Duplicate IDs inside the file or already in SQL reject the
  whole batch. No skip/update/overwrite behavior is hidden in import.

## Local SQL connection

`Web.config` uses Windows authentication for `.\SQLEXPRESS` / `ReportingModuleDemo`.
No password is stored. The process account must have appropriate access to that
database. TrustServerCertificate is a local-demo setting. This remains a local
development application; deployment authentication/permissions and production
connection configuration are not implemented by Phase 3.

## Validation

```powershell
.\tests\run-phase3.ps1
```

Run against the existing synthetic demo database. Service tests use uniquely
named temporary master rows and remove them in finally blocks. They also verify
that referenced HANG001 and DVDEMO are protected. No documents are created.

The test runner uses ASP.NET's ApplicationHost/SimpleWorkerRequest to execute real
page rendering and postbacks without IIS. It checks filtering, refresh, editor
validation/cancel, selection, create/update/delete against SQL, all four catalogs,
shared navigation and invalid routes. It is a test harness, not an alternative
application server or a visual browser test.

Independent validation also opened all four exported workbook types with the
environment's existing openpyxl package, checked numeric/date types and safe text,
and imported a workbook created by openpyxl. openpyxl is not an application dependency.

Release build and ASP.NET precompilation pass. Visual browser/keyboard/viewport
checks remain pending IIS/IIS Express availability; the missing official web
targets are still reported by the build. Hosting tools were not installed.

## Scope remaining

No warehouse documents, stock-posting logic, report queries, payment processing,
PDF or print functionality was added. HANG001 remains at opening 100 with no
movements. Phase 4 implements and verifies the inventory vertical slice.
