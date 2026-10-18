SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
BEGIN TRANSACTION;
DECLARE @Lock int;
EXEC @Lock=sys.sp_getapplock @Resource=N'ReportingModule.Schema',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;
IF @Lock<0 THROW 51800,'Cannot acquire migration lock.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaVersion WHERE Version=7) THROW 51800,'Purchase workflow schema version 7 required.',1;
IF EXISTS(SELECT ChungTuNhapMuaId FROM dbo.ChungTuThuongMai WHERE ChungTuNhapMuaId IS NOT NULL AND TrangThai<>'CANCELLED' GROUP BY ChungTuNhapMuaId HAVING COUNT(*)>1)
 THROW 51801,'A purchase receipt is linked to multiple invoices. Resolve duplicates before applying migration 009.',1;
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.ChungTuThuongMai') AND name=N'UX_CTTM_PurchaseReceipt')
 CREATE UNIQUE INDEX UX_CTTM_PurchaseReceipt ON dbo.ChungTuThuongMai(ChungTuNhapMuaId)
  WHERE ChungTuNhapMuaId IS NOT NULL AND TrangThai<>'CANCELLED';
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaVersion WHERE Version=8) INSERT dbo.SchemaVersion(Version) VALUES(8);
COMMIT;
PRINT 'Schema version 8 ready (one purchase invoice per receipt).';
GO
