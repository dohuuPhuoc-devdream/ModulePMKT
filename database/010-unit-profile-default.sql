SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
BEGIN TRANSACTION;
DECLARE @Lock int;
EXEC @Lock=sys.sp_getapplock @Resource=N'ReportingModule.Schema',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;
IF @Lock<0 THROW 51900,'Cannot acquire migration lock.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaVersion WHERE Version=8) THROW 51901,'Schema version 8 required.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaVersion WHERE Version=9)
BEGIN
 IF COL_LENGTH(N'dbo.AppUserUnit',N'IsDefault') IS NULL
  ALTER TABLE dbo.AppUserUnit ADD IsDefault bit NOT NULL CONSTRAINT DF_AppUserUnit_IsDefault DEFAULT(0) WITH VALUES;
 IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.AppUserUnit') AND name=N'UX_AppUserUnit_Default')
  EXEC(N'CREATE UNIQUE INDEX UX_AppUserUnit_Default ON dbo.AppUserUnit(UserId) WHERE IsDefault=1');
 IF COL_LENGTH(N'dbo.DonVi',N'TenPhapLy') IS NULL ALTER TABLE dbo.DonVi ADD TenPhapLy nvarchar(300) NULL;
 IF COL_LENGTH(N'dbo.DonVi',N'DiaChi') IS NULL ALTER TABLE dbo.DonVi ADD DiaChi nvarchar(300) NULL;
 IF COL_LENGTH(N'dbo.DonVi',N'MaSoThue') IS NULL ALTER TABLE dbo.DonVi ADD MaSoThue varchar(20) NULL;
 IF COL_LENGTH(N'dbo.DonVi',N'NguoiDaiDien') IS NULL ALTER TABLE dbo.DonVi ADD NguoiDaiDien nvarchar(120) NULL;
 IF COL_LENGTH(N'dbo.DonVi',N'DienThoai') IS NULL ALTER TABLE dbo.DonVi ADD DienThoai varchar(30) NULL;
 IF COL_LENGTH(N'dbo.DonVi',N'Email') IS NULL ALTER TABLE dbo.DonVi ADD Email nvarchar(254) NULL;
 INSERT dbo.SchemaVersion(Version) VALUES(9);
END;
COMMIT;
PRINT 'Schema version 9 ready (working-unit defaults and legal profile fields).';
GO
