SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
BEGIN TRANSACTION;
DECLARE @Lock int;
EXEC @Lock=sys.sp_getapplock @Resource=N'ReportingModule.Schema',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;
IF @Lock<0 THROW 51700,'Cannot acquire migration lock.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaVersion WHERE Version=6) THROW 51700,'Security schema version 6 required.',1;
IF COL_LENGTH('dbo.ChungTuKho','MaNCC') IS NULL
 EXEC(N'ALTER TABLE dbo.ChungTuKho ADD MaNCC varchar(20) NULL CONSTRAINT FK_CTK_NCC REFERENCES dbo.NhaCungCap(MaNCC)');
IF COL_LENGTH('dbo.ChungTuThuongMai','ChungTuNhapMuaId') IS NULL
 EXEC(N'ALTER TABLE dbo.ChungTuThuongMai ADD ChungTuNhapMuaId bigint NULL CONSTRAINT FK_CTTM_PurchaseReceipt REFERENCES dbo.ChungTuKho(ChungTuKhoId)');
IF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.ChungTuKho') AND name='CK_CTK_PurchaseReceiptSupplier')
 EXEC(N'ALTER TABLE dbo.ChungTuKho ADD CONSTRAINT CK_CTK_PurchaseReceiptSupplier CHECK(MaNCC IS NULL OR Loai=''N'')');
GO
-- Preserve the earlier demo behavior as historical combined facts. Future receipts and invoices are separate.
UPDATE k SET MaNCC=h.MaNCC
 FROM dbo.ChungTuKho k JOIN dbo.ChungTuThuongMai h ON h.ChungTuId=k.ChungTuThuongMaiId
 WHERE h.Loai='PURCHASE_INVOICE' AND k.Loai='N' AND k.MaNCC IS NULL;
UPDATE h SET ChungTuNhapMuaId=k.ChungTuKhoId
 FROM dbo.ChungTuThuongMai h JOIN dbo.ChungTuKho k ON k.ChungTuThuongMaiId=h.ChungTuId
 WHERE h.Loai='PURCHASE_INVOICE' AND h.ChungTuNhapMuaId IS NULL;
GO
CREATE OR ALTER PROCEDURE dbo.SaveStockDocument
 @Id bigint=NULL, @Version binary(8)=NULL,
 @Unit varchar(20), @Kind char(1), @Number varchar(30), @Date date,
 @Note nvarchar(500)=NULL, @Post bit=0, @Lines dbo.StockLines READONLY,
 @SavedId bigint=NULL OUTPUT, @ReturnResult bit=1,
 @Supplier varchar(20)=NULL, @PurchaseReceipt bit=0,
 @Warehouse varchar(20)=NULL, @DeliveryPerson nvarchar(120)=NULL
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON; SET ANSI_WARNINGS ON; SET ARITHABORT ON;
 SET CONCAT_NULL_YIELDS_NULL ON; SET NUMERIC_ROUNDABORT OFF;
 IF @Kind IS NULL OR @Kind NOT IN ('N','X') OR @Date IS NULL OR @Post IS NULL
  OR @PurchaseReceipt IS NULL OR NULLIF(LTRIM(RTRIM(@Number)),'') IS NULL THROW 51701,N'Loại, số phiếu và ngày là bắt buộc.',1;
 IF (@PurchaseReceipt=1 AND (@Kind<>'N' OR NULLIF(LTRIM(RTRIM(@Supplier)),'') IS NULL))
  OR (@PurchaseReceipt=0 AND @Supplier IS NOT NULL) THROW 51702,N'Nhà cung cấp chỉ áp dụng cho phiếu nhập mua hàng.',1;
 IF (@PurchaseReceipt=1 AND (NULLIF(LTRIM(RTRIM(@Warehouse)),'') IS NULL OR LEN(@DeliveryPerson)>120))
  OR (@PurchaseReceipt=0 AND (@Warehouse IS NOT NULL OR @DeliveryPerson IS NOT NULL)) THROW 51702,N'Kho hoặc người giao hàng không hợp lệ.',1;
 IF NOT EXISTS(SELECT 1 FROM @Lines) OR (SELECT COUNT(*) FROM @Lines)>100 OR EXISTS(SELECT 1 FROM @Lines WHERE Dong<=0 OR SoLuong<=0 OR DonGia<0)
  THROW 51703,N'Phiếu cần 1-100 dòng; số lượng phải dương và đơn giá không âm.',1;
 BEGIN TRY
  BEGIN TRANSACTION;
  DECLARE @OpeningDate date;
  SELECT @OpeningDate=NgayBatDau FROM dbo.DonVi WITH(XLOCK,HOLDLOCK) WHERE MaDonVi=@Unit;
  IF @OpeningDate IS NULL OR @Date<@OpeningDate THROW 51704,N'Đơn vị không hợp lệ hoặc ngày trước ngày bắt đầu.',1;
  IF @PurchaseReceipt=1 AND NOT EXISTS(SELECT 1 FROM dbo.NhaCungCap WHERE MaNCC=@Supplier) THROW 51705,N'Nhà cung cấp không tồn tại.',1;
  IF @PurchaseReceipt=1 AND NOT EXISTS(SELECT 1 FROM dbo.Kho WHERE MaDonVi=@Unit AND MaKho=@Warehouse) THROW 51705,N'Kho không thuộc đơn vị đang làm việc.',1;
  IF EXISTS(SELECT 1 FROM @Lines l LEFT JOIN dbo.HangHoa h ON h.MaHang=l.MaHang WHERE h.MaHang IS NULL) THROW 51706,N'Mã hàng không tồn tại.',1;
  IF @Id IS NULL
  BEGIN
   INSERT dbo.ChungTuKho(MaDonVi,Loai,SoChungTu,Ngay,MaNCC,DienGiai,MaKho,NguoiGiaoHang)
    VALUES(@Unit,@Kind,LTRIM(RTRIM(@Number)),@Date,@Supplier,@Note,COALESCE(@Warehouse,'KHO01'),@DeliveryPerson);
   SET @Id=SCOPE_IDENTITY();
  END
  ELSE
  BEGIN
   UPDATE dbo.ChungTuKho SET SoChungTu=LTRIM(RTRIM(@Number)),Ngay=@Date,MaNCC=@Supplier,DienGiai=@Note,MaKho=COALESCE(@Warehouse,MaKho),NguoiGiaoHang=@DeliveryPerson
    WHERE ChungTuKhoId=@Id AND PhienBan=@Version AND MaDonVi=@Unit AND Loai=@Kind AND TrangThai='DRAFT' AND ChungTuThuongMaiId IS NULL;
   IF @@ROWCOUNT<>1 THROW 51707,N'Phiếu đã thay đổi, đã ghi sổ hoặc không được phép sửa.',1;
   DELETE dbo.CTChungTuKho WHERE ChungTuKhoId=@Id;
  END;
  INSERT dbo.CTChungTuKho(ChungTuKhoId,Dong,MaHang,SoLuong,DonGia) SELECT @Id,Dong,MaHang,SoLuong,DonGia FROM @Lines;
  IF @Post=1
  BEGIN
   UPDATE dbo.ChungTuKho SET TrangThai='POSTED' WHERE ChungTuKhoId=@Id;
   DECLARE @Negative bit=0;
   ;WITH Moves AS(
    SELECT d.MaHang,h.Ngay,h.ChungTuKhoId,SUM(CASE h.Loai WHEN 'N' THEN d.SoLuong ELSE -d.SoLuong END) AS Delta
    FROM dbo.ChungTuKho h JOIN dbo.CTChungTuKho d ON d.ChungTuKhoId=h.ChungTuKhoId
    WHERE h.MaDonVi=@Unit AND h.TrangThai='POSTED' AND EXISTS(SELECT 1 FROM @Lines l WHERE l.MaHang=d.MaHang)
    GROUP BY d.MaHang,h.Ngay,h.ChungTuKhoId
   ), Running AS(
    SELECT MaHang,SUM(Delta) OVER(PARTITION BY MaHang ORDER BY Ngay,ChungTuKhoId ROWS UNBOUNDED PRECEDING) AS Balance FROM Moves
   )
   SELECT TOP(1) @Negative=1 FROM Running r LEFT JOIN dbo.TonDauKy o WITH(HOLDLOCK) ON o.MaDonVi=@Unit AND o.MaHang=r.MaHang
   WHERE CONVERT(decimal(28,3),r.Balance)+COALESCE(o.SoLuong,0)<0;
   IF @Negative=1 THROW 51708,N'Không đủ tồn kho. Ghi sổ sẽ làm tồn âm tại một thời điểm.',1;
  END;
  COMMIT; SET @SavedId=@Id; IF @ReturnResult=1 SELECT @Id AS ChungTuKhoId;
 END TRY
 BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;
END;
GO
CREATE OR ALTER PROCEDURE dbo.SaveCommerceDocument
 @Id bigint=NULL,@Version binary(8)=NULL,@Unit varchar(20),@Kind varchar(20),
 @Number varchar(30),@Date date,@Partner varchar(20),@Note nvarchar(500)=NULL,
 @Post bit=0,@Lines dbo.StockLines READONLY,@PurchaseReceiptId bigint=NULL
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON; SET ANSI_WARNINGS ON; SET ARITHABORT ON;
 SET CONCAT_NULL_YIELDS_NULL ON; SET NUMERIC_ROUNDABORT OFF;
 IF @Kind IS NULL OR @Kind NOT IN ('SALES_ORDER','SALES_INVOICE','PURCHASE_ORDER','PURCHASE_INVOICE') OR @Date IS NULL OR @Post IS NULL OR NULLIF(LTRIM(RTRIM(@Number)),'') IS NULL
  THROW 51709,N'Loại, số và ngày chứng từ không hợp lệ.',1;
 IF NOT EXISTS(SELECT 1 FROM @Lines) OR (SELECT COUNT(*) FROM @Lines)>100 OR EXISTS(SELECT 1 FROM @Lines WHERE Dong<=0 OR SoLuong<=0 OR DonGia<0)
  THROW 51710,N'Cần 1-100 dòng; số lượng dương và đơn giá không âm.',1;
 IF @Kind<>'PURCHASE_INVOICE' AND @PurchaseReceiptId IS NOT NULL THROW 51711,N'Chỉ hóa đơn mua hàng được liên kết phiếu nhập mua.',1;
 IF @Kind='PURCHASE_INVOICE' AND @Post=1 AND @PurchaseReceiptId IS NULL THROW 51712,N'Chọn phiếu nhập mua đã ghi sổ trước khi ghi sổ hóa đơn mua.',1;
 BEGIN TRY
  BEGIN TRANSACTION;
  DECLARE @Opening date,@Sales bit=CASE WHEN @Kind LIKE 'SALES_%' THEN 1 ELSE 0 END;
  SELECT @Opening=NgayBatDau FROM dbo.DonVi WITH(XLOCK,HOLDLOCK) WHERE MaDonVi=@Unit;
  IF @Opening IS NULL OR @Date<@Opening THROW 51713,N'Đơn vị/ngày chứng từ không hợp lệ.',1;
  IF (@Sales=1 AND NOT EXISTS(SELECT 1 FROM dbo.KhachHang WHERE MaKH=@Partner)) OR (@Sales=0 AND NOT EXISTS(SELECT 1 FROM dbo.NhaCungCap WHERE MaNCC=@Partner))
   THROW 51714,N'Khách hàng/nhà cung cấp không tồn tại.',1;
  IF EXISTS(SELECT 1 FROM @Lines l LEFT JOIN dbo.HangHoa i ON i.MaHang=l.MaHang WHERE i.MaHang IS NULL) THROW 51715,N'Mã hàng không tồn tại.',1;
  IF @PurchaseReceiptId IS NOT NULL AND NOT EXISTS(
   SELECT 1 FROM dbo.ChungTuKho WITH(UPDLOCK,HOLDLOCK) WHERE ChungTuKhoId=@PurchaseReceiptId AND MaDonVi=@Unit AND Loai='N' AND TrangThai='POSTED' AND MaNCC=@Partner)
   THROW 51716,N'Phiếu nhập mua phải đã ghi sổ, cùng đơn vị và cùng nhà cung cấp.',1;
  DECLARE @KH varchar(20)=CASE WHEN @Sales=1 THEN @Partner END,@NCC varchar(20)=CASE WHEN @Sales=0 THEN @Partner END;
  IF @Id IS NULL
  BEGIN
   INSERT dbo.ChungTuThuongMai(MaDonVi,Loai,SoChungTu,Ngay,MaKH,MaNCC,DienGiai,ChungTuNhapMuaId)
    VALUES(@Unit,@Kind,LTRIM(RTRIM(@Number)),@Date,@KH,@NCC,@Note,@PurchaseReceiptId);
   SET @Id=SCOPE_IDENTITY();
  END
  ELSE
  BEGIN
   UPDATE dbo.ChungTuThuongMai SET SoChungTu=LTRIM(RTRIM(@Number)),Ngay=@Date,MaKH=@KH,MaNCC=@NCC,DienGiai=@Note,ChungTuNhapMuaId=@PurchaseReceiptId
    WHERE ChungTuId=@Id AND MaDonVi=@Unit AND Loai=@Kind AND PhienBan=@Version AND TrangThai='DRAFT' AND DonHangNguonId IS NULL;
   IF @@ROWCOUNT<>1 THROW 51717,N'Chứng từ đã thay đổi hoặc đã ghi sổ. Hãy chọn lại.',1;
   DELETE dbo.CTChungTuThuongMai WHERE ChungTuId=@Id;
  END;
  INSERT dbo.CTChungTuThuongMai(ChungTuId,Dong,MaHang,SoLuong,DonGia) SELECT @Id,Dong,MaHang,SoLuong,DonGia FROM @Lines;
  IF @Post=1
  BEGIN
   IF @Kind='SALES_INVOICE'
   BEGIN
    DECLARE @StockId bigint,@StockNumber varchar(30)='TM-'+CONVERT(varchar(20),@Id);
    EXEC dbo.SaveStockDocument @Unit=@Unit,@Kind='X',@Number=@StockNumber,@Date=@Date,@Note=@Note,@Post=1,@Lines=@Lines,@SavedId=@StockId OUTPUT,@ReturnResult=0;
    UPDATE dbo.ChungTuKho SET ChungTuThuongMaiId=@Id WHERE ChungTuKhoId=@StockId;
   END;
   UPDATE dbo.ChungTuThuongMai SET TrangThai='POSTED' WHERE ChungTuId=@Id;
  END;
  COMMIT; SELECT @Id AS ChungTuId;
 END TRY
 BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;
END;
GO
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaVersion WHERE Version=7) INSERT dbo.SchemaVersion(Version) VALUES(7);
COMMIT;
PRINT 'Purchase receipt and payable invoice workflows separated (schema version 7).';
