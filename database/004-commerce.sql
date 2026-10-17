SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
BEGIN TRANSACTION;
DECLARE @Lock int;
EXEC @Lock=sys.sp_getapplock @Resource=N'ReportingModule.Schema',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;
IF @Lock<0 THROW 51300,'Cannot acquire migration lock.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaVersion WHERE Version=2) THROW 51300,'Inventory schema required.',1;
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.ChungTuKho') AND name='UX_CTK_Commercial')
 CREATE UNIQUE INDEX UX_CTK_Commercial ON dbo.ChungTuKho(ChungTuThuongMaiId) WHERE ChungTuThuongMaiId IS NOT NULL;
GO
CREATE OR ALTER PROCEDURE dbo.SaveCommerceDocument
 @Id bigint=NULL,@Version binary(8)=NULL,@Unit varchar(20),@Kind varchar(20),
 @Number varchar(30),@Date date,@Partner varchar(20),@Note nvarchar(500)=NULL,
 @Post bit=0,@Lines dbo.StockLines READONLY
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON; SET ANSI_WARNINGS ON; SET ARITHABORT ON;
 SET CONCAT_NULL_YIELDS_NULL ON; SET NUMERIC_ROUNDABORT OFF;
 IF @Kind IS NULL OR @Kind NOT IN ('SALES_ORDER','SALES_INVOICE','PURCHASE_ORDER','PURCHASE_INVOICE')
  OR @Date IS NULL OR @Post IS NULL OR NULLIF(LTRIM(RTRIM(@Number)),'') IS NULL
  THROW 51301,N'Loại, số và ngày chứng từ không hợp lệ.',1;
 IF NOT EXISTS(SELECT 1 FROM @Lines) OR (SELECT COUNT(*) FROM @Lines)>100
  OR EXISTS(SELECT 1 FROM @Lines WHERE Dong<=0 OR SoLuong<=0 OR DonGia<0)
  THROW 51302,N'Cần 1-100 dòng; số lượng dương và đơn giá không âm.',1;
 BEGIN TRY
  BEGIN TRANSACTION;
  DECLARE @Opening date,@Sales bit=CASE WHEN @Kind LIKE 'SALES_%' THEN 1 ELSE 0 END;
  SELECT @Opening=NgayBatDau FROM dbo.DonVi WITH(XLOCK,HOLDLOCK) WHERE MaDonVi=@Unit;
  IF @Opening IS NULL OR @Date<@Opening THROW 51303,N'Đơn vị/ngày chứng từ không hợp lệ.',1;
  IF (@Sales=1 AND NOT EXISTS(SELECT 1 FROM dbo.KhachHang WHERE MaKH=@Partner))
   OR (@Sales=0 AND NOT EXISTS(SELECT 1 FROM dbo.NhaCungCap WHERE MaNCC=@Partner))
   THROW 51304,N'Khách hàng/nhà cung cấp không tồn tại.',1;
  IF EXISTS(SELECT 1 FROM @Lines l LEFT JOIN dbo.HangHoa i ON i.MaHang=l.MaHang WHERE i.MaHang IS NULL)
   THROW 51305,N'Mã hàng không tồn tại.',1;
  DECLARE @KH varchar(20)=CASE WHEN @Sales=1 THEN @Partner END,@NCC varchar(20)=CASE WHEN @Sales=0 THEN @Partner END;
  IF @Id IS NULL
  BEGIN
   INSERT dbo.ChungTuThuongMai(MaDonVi,Loai,SoChungTu,Ngay,MaKH,MaNCC,DienGiai)
    VALUES(@Unit,@Kind,LTRIM(RTRIM(@Number)),@Date,@KH,@NCC,@Note);
   SET @Id=SCOPE_IDENTITY();
  END
  ELSE
  BEGIN
   UPDATE dbo.ChungTuThuongMai SET SoChungTu=LTRIM(RTRIM(@Number)),Ngay=@Date,MaKH=@KH,MaNCC=@NCC,DienGiai=@Note
    WHERE ChungTuId=@Id AND MaDonVi=@Unit AND Loai=@Kind AND PhienBan=@Version AND TrangThai='DRAFT' AND DonHangNguonId IS NULL;
   IF @@ROWCOUNT<>1 THROW 51306,N'Chứng từ đã thay đổi hoặc đã ghi sổ. Hãy chọn lại.',1;
   DELETE dbo.CTChungTuThuongMai WHERE ChungTuId=@Id;
  END;
  INSERT dbo.CTChungTuThuongMai(ChungTuId,Dong,MaHang,SoLuong,DonGia) SELECT @Id,Dong,MaHang,SoLuong,DonGia FROM @Lines;
  IF @Post=1
  BEGIN
   IF @Kind IN ('SALES_INVOICE','PURCHASE_INVOICE')
   BEGIN
    DECLARE @StockId bigint,@StockKind char(1)=CASE WHEN @Sales=1 THEN 'X' ELSE 'N' END,
     @StockNumber varchar(30)='TM-'+CONVERT(varchar(20),@Id);
    EXEC dbo.SaveStockDocument @Unit=@Unit,@Kind=@StockKind,@Number=@StockNumber,@Date=@Date,@Note=@Note,
     @Post=1,@Lines=@Lines,@SavedId=@StockId OUTPUT,@ReturnResult=0;
    UPDATE dbo.ChungTuKho SET ChungTuThuongMaiId=@Id WHERE ChungTuKhoId=@StockId;
   END;
   UPDATE dbo.ChungTuThuongMai SET TrangThai='POSTED' WHERE ChungTuId=@Id;
  END;
  COMMIT;
  SELECT @Id AS ChungTuId;
 END TRY
 BEGIN CATCH
  IF XACT_STATE()<>0 ROLLBACK;
  THROW;
 END CATCH;
END;
GO
CREATE OR ALTER PROCEDURE dbo.DeleteCommerceDraft @Id bigint,@Version binary(8),@Unit varchar(20),@Kind varchar(20)
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 BEGIN TRY
  BEGIN TRANSACTION;
  DECLARE @Found varchar(20);
  SELECT @Found=MaDonVi FROM dbo.DonVi WITH(XLOCK,HOLDLOCK) WHERE MaDonVi=@Unit;
  IF @Found IS NULL THROW 51303,N'Đơn vị không tồn tại.',1;
  IF NOT EXISTS(SELECT 1 FROM dbo.ChungTuThuongMai WITH(UPDLOCK,HOLDLOCK) WHERE ChungTuId=@Id AND MaDonVi=@Unit AND Loai=@Kind AND PhienBan=@Version AND TrangThai='DRAFT')
   THROW 51306,N'Chỉ xóa được chứng từ nháp chưa thay đổi.',1;
  DELETE dbo.CTChungTuThuongMai WHERE ChungTuId=@Id;
  DELETE dbo.ChungTuThuongMai WHERE ChungTuId=@Id;
  COMMIT;
 END TRY
 BEGIN CATCH
  IF XACT_STATE()<>0 ROLLBACK;
  THROW;
 END CATCH;
END;
GO
CREATE OR ALTER PROCEDURE dbo.CommerceReport
 @Unit varchar(20),@From date,@To date,@Partner varchar(20)=NULL,@Mode varchar(20)
AS
BEGIN
 SET NOCOUNT ON;
 IF @From IS NULL OR @To IS NULL OR @From>@To OR @Mode IS NULL
  OR @Mode NOT IN ('sales-detail','revenue','sales-analysis','purchase-detail','purchase-analysis')
  THROW 51307,N'Điều kiện báo cáo không hợp lệ.',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.DonVi WHERE MaDonVi=@Unit AND NgayBatDau<=@From)
  THROW 51303,N'Đơn vị hoặc kỳ báo cáo không hợp lệ.',1;
 SET @Partner=NULLIF(LTRIM(RTRIM(@Partner)),'');
 DECLARE @Sales bit=CASE WHEN @Mode IN ('sales-detail','revenue','sales-analysis') THEN 1 ELSE 0 END;
 IF @Partner IS NOT NULL AND ((@Sales=1 AND NOT EXISTS(SELECT 1 FROM dbo.KhachHang WHERE MaKH=@Partner)) OR (@Sales=0 AND NOT EXISTS(SELECT 1 FROM dbo.NhaCungCap WHERE MaNCC=@Partner)))
  THROW 51304,N'Khách hàng/nhà cung cấp không tồn tại.',1;
 SELECT h.Ngay,h.SoChungTu,COALESCE(h.MaKH,h.MaNCC) AS MaDoiTac,COALESCE(k.TenKH,n.TenNCC) AS TenDoiTac,
  d.MaHang,i.TenHang,i.DonViTinh,d.SoLuong,d.DonGia,d.ThanhTien,h.ChungTuId,d.Dong
 INTO #Result
 FROM dbo.ChungTuThuongMai h JOIN dbo.CTChungTuThuongMai d ON d.ChungTuId=h.ChungTuId
 JOIN dbo.HangHoa i ON i.MaHang=d.MaHang LEFT JOIN dbo.KhachHang k ON k.MaKH=h.MaKH LEFT JOIN dbo.NhaCungCap n ON n.MaNCC=h.MaNCC
 WHERE h.MaDonVi=@Unit AND h.TrangThai='POSTED' AND h.Loai=CASE WHEN @Sales=1 THEN 'SALES_INVOICE' ELSE 'PURCHASE_INVOICE' END
 AND h.Ngay>=@From AND h.Ngay<=@To AND (@Partner IS NULL OR COALESCE(h.MaKH,h.MaNCC)=@Partner);
 IF @Mode IN ('sales-detail','purchase-detail')
  SELECT Ngay,SoChungTu,MaDoiTac,TenDoiTac,MaHang,TenHang,DonViTinh,SoLuong,DonGia,ThanhTien FROM #Result ORDER BY Ngay,ChungTuId,Dong;
 ELSE IF @Mode='revenue'
  SELECT MaDoiTac,TenDoiTac,COUNT(DISTINCT ChungTuId) AS SoHoaDon,SUM(SoLuong) AS SoLuong,SUM(ThanhTien) AS ThanhTien
  FROM #Result GROUP BY MaDoiTac,TenDoiTac ORDER BY MaDoiTac;
 ELSE
  SELECT DATEFROMPARTS(YEAR(Ngay),MONTH(Ngay),1) AS Thang,MaHang,TenHang,DonViTinh,SUM(SoLuong) AS SoLuong,SUM(ThanhTien) AS ThanhTien
  FROM #Result GROUP BY YEAR(Ngay),MONTH(Ngay),MaHang,TenHang,DonViTinh ORDER BY Thang,MaHang;
END;
GO
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaVersion WHERE Version=3) INSERT dbo.SchemaVersion(Version) VALUES(3);
COMMIT;
PRINT 'Phase 5 commercial procedures ready (schema version 3).';
