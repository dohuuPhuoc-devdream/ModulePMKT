:On Error exit
USE ReportingModuleDemo;
GO
SET NOCOUNT ON;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaVersion WHERE Version=2) THROW 51300,'Inventory migration missing.',1;
DECLARE @Opening decimal(28,3),@In decimal(28,3),@Out decimal(28,3);
SELECT @Opening=SoLuong FROM dbo.TonDauKy WHERE MaDonVi='DVDEMO' AND MaHang='HANG001';
SELECT @In=COALESCE(SUM(CASE WHEN h.Loai='N' THEN d.SoLuong ELSE 0 END),0),
 @Out=COALESCE(SUM(CASE WHEN h.Loai='X' THEN d.SoLuong ELSE 0 END),0)
FROM dbo.ChungTuKho h JOIN dbo.CTChungTuKho d ON d.ChungTuKhoId=h.ChungTuKhoId
WHERE h.MaDonVi='DVDEMO' AND h.TrangThai='POSTED' AND d.MaHang='HANG001'
 AND h.Ngay>='20260101' AND h.Ngay<='20261231';
IF @Opening IS NULL OR @Opening<>100 OR @In<>50 OR @Out<>20 OR @Opening+@In-@Out<>130
 THROW 51301,'Acceptance demo differs from 100 + 50 - 20 = 130.',1;
IF EXISTS(SELECT 1 FROM dbo.ChungTuKho WHERE MaDonVi='DVDEMO' AND SoChungTu='REJECT200')
 THROW 51302,'Rejected UI issue left a header.',1;
IF EXISTS(SELECT 1 FROM dbo.CTChungTuKho d LEFT JOIN dbo.ChungTuKho h ON h.ChungTuKhoId=d.ChungTuKhoId WHERE h.ChungTuKhoId IS NULL)
 THROW 51303,'Orphan detail found.',1;
IF EXISTS(SELECT 1 FROM dbo.ChungTuKho h WHERE h.TrangThai='POSTED'
 AND NOT EXISTS(SELECT 1 FROM dbo.CTChungTuKho d WHERE d.ChungTuKhoId=h.ChungTuKhoId))
 THROW 51304,'Posted document without details.',1;
DECLARE @Report table(MaHang varchar(20),TenHang nvarchar(200),DonViTinh nvarchar(30),TonDau decimal(28,3),Nhap decimal(28,3),Xuat decimal(28,3),TonCuoi decimal(28,3));
INSERT @Report EXEC dbo.InventoryReport @Unit='DVDEMO',@From='20260101',@To='20261231',@Item='HANG001',@Mode='T';
IF NOT EXISTS(SELECT 1 FROM @Report WHERE TonDau=@Opening AND Nhap=@In AND Xuat=@Out AND TonCuoi=@Opening+@In-@Out)
 THROW 51305,'Report differs from independent SQL.',1;
SELECT * FROM @Report;
PRINT 'PASS: independent SQL reconciles HANG001; no failed-issue header or orphan details.';
DBCC CHECKCONSTRAINTS WITH ALL_CONSTRAINTS;

