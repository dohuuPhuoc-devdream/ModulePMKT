SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
BEGIN TRANSACTION;
DECLARE @Lock int;
EXEC @Lock=sys.sp_getapplock @Resource=N'ReportingModule.Schema',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;
IF @Lock<0 THROW 52100,'Cannot acquire migration lock.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaVersion WHERE Version=13) THROW 52100,'Schema version 13 required.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaVersion WHERE Version=14)
BEGIN
 IF EXISTS(SELECT 1 FROM dbo.ChungTuKho WHERE MaKho<>'KHO01')
  THROW 52101,'Non-default warehouse documents exist. Reconcile their quantities before enabling the single-warehouse policy.',1;
 IF EXISTS(SELECT 1 FROM dbo.DonVi d WHERE NOT EXISTS(SELECT 1 FROM dbo.Kho k WHERE k.MaDonVi=d.MaDonVi AND k.MaKho='KHO01'))
  THROW 52102,'Every working unit must have the default warehouse KHO01.',1;
 IF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.ChungTuKho') AND name=N'CK_ChungTuKho_SingleWarehouse')
  ALTER TABLE dbo.ChungTuKho WITH CHECK ADD CONSTRAINT CK_ChungTuKho_SingleWarehouse CHECK(MaKho='KHO01');
 INSERT dbo.SchemaVersion(Version) VALUES(14);
END;
COMMIT;
PRINT 'Schema version 14 ready (inventory movements restricted to KHO01).';
GO
