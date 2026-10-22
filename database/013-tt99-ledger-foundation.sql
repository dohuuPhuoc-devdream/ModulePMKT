SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
GO
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaVersion WHERE Version=12)
BEGIN
 BEGIN TRANSACTION;
 DECLARE @Lock int;
 EXEC @Lock=sys.sp_getapplock @Resource=N'ReportingModule.Schema',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;
 IF @Lock<0 THROW 52200,'Cannot acquire migration lock.',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.SchemaVersion WHERE Version=11) THROW 52201,'Schema version 11 required.',1;
 -- VND amounts are stored per line as whole đồng using SQL ROUND (midpoints away from zero).
 ALTER TABLE dbo.CTChungTuThuongMai DROP COLUMN ThanhTien;
 ALTER TABLE dbo.CTChungTuThuongMai ADD ThanhTien AS CONVERT(decimal(28,4),ROUND(SoLuong*DonGia,0)) PERSISTED;
 ALTER TABLE dbo.CTChungTuKho DROP COLUMN ThanhTien;
 ALTER TABLE dbo.CTChungTuKho ADD ThanhTien AS CONVERT(decimal(28,4),ROUND(SoLuong*DonGia,0)) PERSISTED;
 ALTER TABLE dbo.TaiKhoan ADD MaTKCha varchar(20) NULL;
 ALTER TABLE dbo.TaiKhoan ADD Cap tinyint NOT NULL CONSTRAINT DF_TaiKhoan_Cap DEFAULT(1) WITH VALUES;
 ALTER TABLE dbo.TaiKhoan ADD TinhChat char(1) NULL;
 ALTER TABLE dbo.TaiKhoan ADD ChoPhepHachToan bit NOT NULL CONSTRAINT DF_TaiKhoan_ChoPhepHachToan DEFAULT(1) WITH VALUES;
 ALTER TABLE dbo.TaiKhoan ADD DoiTuong char(2) NULL;
 ALTER TABLE dbo.TaiKhoan ADD HoatDong bit NOT NULL CONSTRAINT DF_TaiKhoan_HoatDong DEFAULT(1) WITH VALUES;
END;
GO
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaVersion WHERE Version=12)
BEGIN
 ALTER TABLE dbo.TaiKhoan ADD CONSTRAINT FK_TaiKhoan_Parent FOREIGN KEY(MaTKCha) REFERENCES dbo.TaiKhoan(MaTK);
 ALTER TABLE dbo.TaiKhoan ADD CONSTRAINT CK_TaiKhoan_Cap CHECK(Cap BETWEEN 1 AND 4);
 ALTER TABLE dbo.TaiKhoan ADD CONSTRAINT CK_TaiKhoan_TinhChat CHECK(TinhChat IS NULL OR TinhChat IN('N','C','L'));
 ALTER TABLE dbo.TaiKhoan ADD CONSTRAINT CK_TaiKhoan_DoiTuong CHECK(DoiTuong IS NULL OR DoiTuong IN('KH','NCC','HH'));
 ALTER TABLE dbo.TaiKhoan ADD CONSTRAINT CK_TaiKhoan_Parent CHECK((Cap=1 AND MaTKCha IS NULL) OR (Cap>1 AND MaTKCha IS NOT NULL));
 ALTER TABLE dbo.CTPhieuKeToan ADD DienGiai nvarchar(500) NULL;
 ALTER TABLE dbo.CTPhieuKeToan ADD MaKH varchar(20) NULL;
 ALTER TABLE dbo.CTPhieuKeToan ADD MaNCC varchar(20) NULL;
END;
GO
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaVersion WHERE Version=12)
BEGIN
 ALTER TABLE dbo.CTPhieuKeToan ADD CONSTRAINT FK_CTPKT_KH FOREIGN KEY(MaKH) REFERENCES dbo.KhachHang(MaKH);
 ALTER TABLE dbo.CTPhieuKeToan ADD CONSTRAINT FK_CTPKT_NCC FOREIGN KEY(MaNCC) REFERENCES dbo.NhaCungCap(MaNCC);
 ALTER TABLE dbo.CTPhieuKeToan ADD CONSTRAINT CK_CTPKT_OnePartner CHECK(MaKH IS NULL OR MaNCC IS NULL);
 ALTER TABLE dbo.PhieuKeToan ADD NguonLoai varchar(20) NULL;
 ALTER TABLE dbo.PhieuKeToan ADD NguonId bigint NULL;
 ALTER TABLE dbo.PhieuKeToan ADD TuDong bit NOT NULL CONSTRAINT DF_PhieuKeToan_TuDong DEFAULT(0) WITH VALUES;
END;
GO
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaVersion WHERE Version=12)
BEGIN
 CREATE UNIQUE INDEX UX_PKT_Source ON dbo.PhieuKeToan(NguonLoai,NguonId) WHERE NguonId IS NOT NULL;
 -- User-defined table types cannot be altered. Keep the old type for external clients.
 IF TYPE_ID(N'dbo.JournalLines2') IS NULL
  EXEC(N'CREATE TYPE dbo.JournalLines2 AS TABLE(Dong int PRIMARY KEY,MaTK varchar(20),PhatSinhNo decimal(28,4),PhatSinhCo decimal(28,4),DienGiai nvarchar(500),MaKH varchar(20),MaNCC varchar(20))');
 INSERT dbo.SchemaVersion(Version) VALUES(12);
 COMMIT;
 PRINT 'TT99 ledger foundation and whole-VND line rounding ready (schema version 12).';
END;
GO
