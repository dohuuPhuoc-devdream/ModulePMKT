SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
BEGIN TRANSACTION;
DECLARE @Lock int;
EXEC @Lock=sys.sp_getapplock @Resource=N'ReportingModule.Schema',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;
IF @Lock<0 THROW 52200,'Cannot acquire migration lock.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaVersion WHERE Version=14) THROW 52200,'Schema version 14 required.',1;
GO
CREATE OR ALTER TRIGGER dbo.TR_CTTM_PurchaseInvoiceReceiptMatch
ON dbo.ChungTuThuongMai
AFTER UPDATE
AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(
  SELECT 1 FROM inserted h JOIN deleted old ON old.ChungTuId=h.ChungTuId
  LEFT JOIN dbo.ChungTuKho receipt ON receipt.ChungTuKhoId=h.ChungTuNhapMuaId
  WHERE old.TrangThai<>'POSTED' AND h.TrangThai='POSTED' AND h.Loai='PURCHASE_INVOICE'
   AND (receipt.ChungTuKhoId IS NULL OR receipt.MaDonVi<>h.MaDonVi OR receipt.Loai<>'N'
    OR receipt.TrangThai<>'POSTED' OR receipt.MaNCC IS NULL OR h.MaNCC IS NULL
    OR receipt.MaNCC<>h.MaNCC OR receipt.Ngay>h.Ngay)
 ) THROW 51718,N'Hóa đơn mua phải khớp đơn vị, nhà cung cấp và ngày với phiếu nhập đã ghi sổ.',1;

 IF EXISTS(
  SELECT 1 FROM inserted h JOIN deleted old ON old.ChungTuId=h.ChungTuId
  WHERE old.TrangThai<>'POSTED' AND h.TrangThai='POSTED' AND h.Loai='PURCHASE_INVOICE'
   AND (
    EXISTS(
     SELECT d.MaHang,d.SoLuong,d.DonGia,COUNT_BIG(*)
     FROM dbo.CTChungTuThuongMai d WHERE d.ChungTuId=h.ChungTuId
     GROUP BY d.MaHang,d.SoLuong,d.DonGia
     EXCEPT
     SELECT d.MaHang,d.SoLuong,d.DonGia,COUNT_BIG(*)
     FROM dbo.CTChungTuKho d WHERE d.ChungTuKhoId=h.ChungTuNhapMuaId
     GROUP BY d.MaHang,d.SoLuong,d.DonGia
    )
    OR EXISTS(
     SELECT d.MaHang,d.SoLuong,d.DonGia,COUNT_BIG(*)
     FROM dbo.CTChungTuKho d WHERE d.ChungTuKhoId=h.ChungTuNhapMuaId
     GROUP BY d.MaHang,d.SoLuong,d.DonGia
     EXCEPT
     SELECT d.MaHang,d.SoLuong,d.DonGia,COUNT_BIG(*)
     FROM dbo.CTChungTuThuongMai d WHERE d.ChungTuId=h.ChungTuId
     GROUP BY d.MaHang,d.SoLuong,d.DonGia
    )
   )
 ) THROW 51719,N'Chi tiết hóa đơn mua phải khớp từng dòng hàng, số lượng và đơn giá của phiếu nhập.',1;
END;
GO
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaVersion WHERE Version=15) INSERT dbo.SchemaVersion(Version) VALUES(15);
COMMIT;
PRINT 'Schema version 15 ready (purchase invoice detail must match its posted receipt).';
GO
