"""Generate the immutable SQL migration from the supplied, Gazette-verified TT99 CSV."""
import csv
from pathlib import Path

root = Path(__file__).resolve().parents[1]
source = root / "database" / "seed" / "tt99-accounts.csv"
target = root / "database" / "014-tt99-accounts.sql"

with source.open(encoding="utf-8-sig", newline="") as stream:
    rows = list(csv.DictReader(stream))

required = {"MaTK", "TenTK", "Cap", "MaTKCha", "SoTTCap1", "LoaiTaiKhoan", "LaTaiKhoanLa"}
if len(rows) != 184 or set(rows[0]) != required:
    raise SystemExit("Expected the verified 184-row TT99 CSV with its seven approved columns.")
codes = {row["MaTK"] for row in rows}
if len(codes) != len(rows) or any(row["MaTKCha"] and row["MaTKCha"] not in codes for row in rows):
    raise SystemExit("CSV codes must be unique and every parent must exist.")

def sql_string(value):
    return "N'" + value.replace("'", "''") + "'"

values = []
for row in rows:
    parent = "NULL" if not row["MaTKCha"] else "'" + row["MaTKCha"] + "'"
    order = "NULL" if not row["SoTTCap1"] else row["SoTTCap1"]
    values.append(
        " (" + ",".join(("'" + row["MaTK"] + "'", sql_string(row["TenTK"]), parent,
                         row["Cap"], order, sql_string(row["LoaiTaiKhoan"]), row["LaTaiKhoanLa"])) + ")"
    )

header = """SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
GO
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaVersion WHERE Version=13)
BEGIN
BEGIN TRANSACTION;
DECLARE @Lock int;
EXEC @Lock=sys.sp_getapplock @Resource=N'ReportingModule.Schema',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;
IF @Lock<0 THROW 52300,'Cannot acquire migration lock.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaVersion WHERE Version=12) THROW 52301,'Schema version 12 required.',1;
ALTER TABLE dbo.TaiKhoan ADD SoTTCap1 int NULL;
ALTER TABLE dbo.TaiKhoan ADD LoaiTaiKhoan nvarchar(100) NULL;
CREATE TABLE #TT99Accounts(MaTK varchar(20) PRIMARY KEY,TenTK nvarchar(200),MaTKCha varchar(20) NULL,Cap tinyint,SoTTCap1 int NULL,LoaiTaiKhoan nvarchar(100),LaTaiKhoanLa bit);
END;
GO
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaVersion WHERE Version=13)
BEGIN
INSERT #TT99Accounts(MaTK,TenTK,MaTKCha,Cap,SoTTCap1,LoaiTaiKhoan,LaTaiKhoanLa) VALUES
"""

footer = """;
IF (SELECT COUNT(*) FROM #TT99Accounts)<>184 THROW 52302,'Unexpected TT99 account seed row count.',1;
IF EXISTS(SELECT 1 FROM dbo.TaiKhoan t JOIN #TT99Accounts a ON a.MaTK=t.MaTK WHERE t.TenTK<>a.TenTK OR ISNULL(t.MaTKCha,'')<>ISNULL(a.MaTKCha,'') OR t.Cap<>a.Cap OR (t.LoaiTaiKhoan IS NOT NULL AND t.LoaiTaiKhoan<>a.LoaiTaiKhoan))
 THROW 52303,'Existing account conflicts with the verified TT99 source; no account was overwritten.',1;
INSERT dbo.TaiKhoan(MaTK,TenTK,MaTKCha,Cap,TinhChat,ChoPhepHachToan,DoiTuong,HoatDong,SoTTCap1,LoaiTaiKhoan)
 SELECT a.MaTK,a.TenTK,a.MaTKCha,a.Cap,NULL,a.LaTaiKhoanLa,NULL,1,a.SoTTCap1,a.LoaiTaiKhoan
 FROM #TT99Accounts a WHERE NOT EXISTS(SELECT 1 FROM dbo.TaiKhoan t WHERE t.MaTK=a.MaTK);
UPDATE t SET SoTTCap1=a.SoTTCap1,LoaiTaiKhoan=a.LoaiTaiKhoan,ChoPhepHachToan=a.LaTaiKhoanLa
 FROM dbo.TaiKhoan t JOIN #TT99Accounts a ON a.MaTK=t.MaTK WHERE t.SoTTCap1 IS NULL OR t.LoaiTaiKhoan IS NULL;
END;
GO
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaVersion WHERE Version=13)
BEGIN

-- Retain historical journal facts while replacing the six internal teaching codes with their TT99 equivalents.
UPDATE d SET MaTK=m.NewCode FROM dbo.CTPhieuKeToan d JOIN (VALUES
 ('TS_TIEN','111'),('TS_KH','131'),('TS_HANG','156'),('NV_NCC','331'),('NV_VON','4111'),('NV_LN','4212')
) m(OldCode,NewCode) ON m.OldCode=d.MaTK;
DELETE oldMap FROM dbo.TaiKhoanBaoCao oldMap JOIN (VALUES
 ('TS_TIEN','111'),('TS_KH','131'),('TS_HANG','156'),('NV_NCC','331'),('NV_VON','4111'),('NV_LN','4212')
) m(OldCode,NewCode) ON oldMap.MaTK=m.OldCode WHERE EXISTS(SELECT 1 FROM dbo.TaiKhoanBaoCao newMap WHERE newMap.MaTK=m.NewCode);
UPDATE oldMap SET MaTK=m.NewCode FROM dbo.TaiKhoanBaoCao oldMap JOIN (VALUES
 ('TS_TIEN','111'),('TS_KH','131'),('TS_HANG','156'),('NV_NCC','331'),('NV_VON','4111'),('NV_LN','4212')
) m(OldCode,NewCode) ON oldMap.MaTK=m.OldCode WHERE NOT EXISTS(SELECT 1 FROM dbo.TaiKhoanBaoCao newMap WHERE newMap.MaTK=m.NewCode);
DELETE oldAccount FROM dbo.TaiKhoan oldAccount JOIN (VALUES
 ('TS_TIEN','111'),('TS_KH','131'),('TS_HANG','156'),('NV_NCC','331'),('NV_VON','4111'),('NV_LN','4212')
) m(OldCode,NewCode) ON oldAccount.MaTK=m.OldCode WHERE NOT EXISTS(SELECT 1 FROM dbo.CTPhieuKeToan d WHERE d.MaTK=oldAccount.MaTK);
IF EXISTS(SELECT 1 FROM dbo.TaiKhoan WHERE MaTK IN('TS_TIEN','TS_KH','TS_HANG','NV_NCC','NV_VON','NV_LN'))
 THROW 52304,'A teaching account remains referenced and could not be mapped.',1;
IF (SELECT COUNT(*) FROM dbo.TaiKhoan t JOIN #TT99Accounts a ON a.MaTK=t.MaTK WHERE t.TenTK=a.TenTK AND t.Cap=a.Cap AND t.ChoPhepHachToan=a.LaTaiKhoanLa)<>184
 THROW 52305,'TT99 account import verification failed.',1;
INSERT dbo.SchemaVersion(Version) VALUES(13);
COMMIT;
PRINT 'Verified TT99 Appendix II accounts loaded; teaching journal facts mapped; schema version 13.';
END;
GO
"""

target.write_text(header + ",\n".join(values) + footer, encoding="utf-8")
print(f"Generated {target.relative_to(root)} with {len(rows)} verified account rows.")
