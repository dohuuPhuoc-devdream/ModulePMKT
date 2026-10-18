SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
BEGIN TRANSACTION;
DECLARE @Lock int;
EXEC @Lock=sys.sp_getapplock @Resource=N'ReportingModule.Schema',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;
IF @Lock<0 THROW 52000,'Cannot acquire migration lock.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaVersion WHERE Version=9) THROW 52001,'Schema version 9 required.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaVersion WHERE Version=10)
BEGIN
 CREATE TABLE dbo.Kho(
  MaDonVi varchar(20) NOT NULL CONSTRAINT FK_Kho_DonVi REFERENCES dbo.DonVi(MaDonVi),
  MaKho varchar(20) NOT NULL,
  TenKho nvarchar(120) NOT NULL,
  CONSTRAINT PK_Kho PRIMARY KEY(MaDonVi,MaKho),
  CONSTRAINT CK_Kho_Required CHECK(LEN(LTRIM(RTRIM(MaKho)))>0 AND LEN(LTRIM(RTRIM(TenKho)))>0));
 INSERT dbo.Kho(MaDonVi,MaKho,TenKho) SELECT MaDonVi,'KHO01',N'Kho chính' FROM dbo.DonVi;
 EXEC(N'CREATE TRIGGER dbo.TR_DonVi_DefaultWarehouse ON dbo.DonVi AFTER INSERT AS
 BEGIN SET NOCOUNT ON; INSERT dbo.Kho(MaDonVi,MaKho,TenKho) SELECT MaDonVi,''KHO01'',N''Kho chính'' FROM inserted; END');
 IF COL_LENGTH(N'dbo.ChungTuKho',N'MaKho') IS NULL
  ALTER TABLE dbo.ChungTuKho ADD MaKho varchar(20) NOT NULL CONSTRAINT DF_ChungTuKho_MaKho DEFAULT('KHO01') WITH VALUES;
 IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_ChungTuKho_Kho')
  EXEC(N'ALTER TABLE dbo.ChungTuKho ADD CONSTRAINT FK_ChungTuKho_Kho FOREIGN KEY(MaDonVi,MaKho) REFERENCES dbo.Kho(MaDonVi,MaKho)');
 IF COL_LENGTH(N'dbo.ChungTuKho',N'NguoiGiaoHang') IS NULL
  ALTER TABLE dbo.ChungTuKho ADD NguoiGiaoHang nvarchar(120) NULL;
 INSERT dbo.SchemaVersion(Version) VALUES(10);
END;
COMMIT;
PRINT 'Schema version 10 ready (one default warehouse per unit and delivery contact).';
GO
