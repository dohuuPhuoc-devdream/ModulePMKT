SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
BEGIN TRANSACTION;
DECLARE @Lock int;
EXEC @Lock=sys.sp_getapplock @Resource=N'ReportingModule.Schema',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;
IF @Lock<0 THROW 51200,'Cannot acquire migration lock.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaVersion WHERE Version=1) THROW 51200,'Schema 1 required.',1;
IF TYPE_ID(N'dbo.StockLines') IS NULL
 EXEC(N'CREATE TYPE dbo.StockLines AS TABLE(Dong int NOT NULL PRIMARY KEY, MaHang varchar(20) NOT NULL, SoLuong decimal(18,3) NOT NULL, DonGia decimal(19,4) NOT NULL)');
GO
CREATE OR ALTER PROCEDURE dbo.SaveStockDocument
 @Id bigint=NULL, @Version binary(8)=NULL,
 @Unit varchar(20), @Kind char(1), @Number varchar(30), @Date date,
 @Note nvarchar(500)=NULL, @Post bit=0, @Lines dbo.StockLines READONLY, @SavedId bigint=NULL OUTPUT, @ReturnResult bit=1
AS
BEGIN
 SET NOCOUNT ON;
 SET XACT_ABORT ON;
 SET ANSI_WARNINGS ON;
 SET ARITHABORT ON;
 SET CONCAT_NULL_YIELDS_NULL ON;
 SET NUMERIC_ROUNDABORT OFF;
 IF @Kind IS NULL OR @Kind NOT IN ('N','X') OR @Date IS NULL OR @Post IS NULL
  OR NULLIF(LTRIM(RTRIM(@Number)),'') IS NULL THROW 51201,N'Loại, số phiếu và ngày là bắt buộc.',1;
 IF NOT EXISTS(SELECT 1 FROM @Lines) OR (SELECT COUNT(*) FROM @Lines)>100
  THROW 51202,N'Phiếu cần từ 1 đến 100 dòng chi tiết.',1;
 IF EXISTS(SELECT 1 FROM @Lines WHERE Dong<=0 OR SoLuong<=0 OR DonGia<0)
  THROW 51203,N'Số lượng phải lớn hơn 0, đơn giá không âm.',1;
 BEGIN TRY
  BEGIN TRANSACTION;
  -- Every stock mutation locks the SAME unit row first, independent of input casing.
  DECLARE @OpeningDate date;
  SELECT @OpeningDate=NgayBatDau FROM dbo.DonVi WITH(XLOCK,HOLDLOCK) WHERE MaDonVi=@Unit;
  IF @OpeningDate IS NULL OR @Date<@OpeningDate THROW 51204,N'Đơn vị không hợp lệ hoặc ngày trước ngày bắt đầu.',1;
  IF EXISTS(SELECT 1 FROM @Lines l LEFT JOIN dbo.HangHoa h ON h.MaHang=l.MaHang WHERE h.MaHang IS NULL)
   THROW 51205,N'Mã hàng không tồn tại.',1;
  IF @Id IS NULL
  BEGIN
   INSERT dbo.ChungTuKho(MaDonVi,Loai,SoChungTu,Ngay,DienGiai)
    VALUES(@Unit,@Kind,LTRIM(RTRIM(@Number)),@Date,@Note);
   SET @Id=SCOPE_IDENTITY();
  END
  ELSE
  BEGIN
   UPDATE dbo.ChungTuKho SET SoChungTu=LTRIM(RTRIM(@Number)),Ngay=@Date,DienGiai=@Note
    WHERE ChungTuKhoId=@Id AND PhienBan=@Version AND MaDonVi=@Unit AND Loai=@Kind
     AND TrangThai='DRAFT' AND ChungTuThuongMaiId IS NULL;
   IF @@ROWCOUNT<>1 THROW 51206,N'Phiếu đã thay đổi, đã ghi sổ hoặc không được phép sửa.',1;
   DELETE dbo.CTChungTuKho WHERE ChungTuKhoId=@Id;
  END;
  INSERT dbo.CTChungTuKho(ChungTuKhoId,Dong,MaHang,SoLuong,DonGia)
   SELECT @Id,Dong,MaHang,SoLuong,DonGia FROM @Lines;
  IF @Post=1
  BEGIN
   UPDATE dbo.ChungTuKho SET TrangThai='POSTED' WHERE ChungTuKhoId=@Id;
   -- Group repeated item lines, then check EVERY historical prefix, not just today's total.
   DECLARE @Negative bit=0;
   ;WITH Moves AS(
    SELECT d.MaHang,h.Ngay,h.ChungTuKhoId,
     SUM(CASE h.Loai WHEN 'N' THEN d.SoLuong ELSE -d.SoLuong END) AS Delta
    FROM dbo.ChungTuKho h
    JOIN dbo.CTChungTuKho d ON d.ChungTuKhoId=h.ChungTuKhoId
    WHERE h.MaDonVi=@Unit AND h.TrangThai='POSTED'
     AND EXISTS(SELECT 1 FROM @Lines l WHERE l.MaHang=d.MaHang)
    GROUP BY d.MaHang,h.Ngay,h.ChungTuKhoId
   ), Running AS(
    SELECT MaHang,SUM(Delta) OVER(PARTITION BY MaHang ORDER BY Ngay,ChungTuKhoId ROWS UNBOUNDED PRECEDING) AS Balance
    FROM Moves
   )
   SELECT TOP(1) @Negative=1 FROM Running r
    LEFT JOIN dbo.TonDauKy o WITH(HOLDLOCK) ON o.MaDonVi=@Unit AND o.MaHang=r.MaHang
    WHERE CONVERT(decimal(28,3),r.Balance)+COALESCE(o.SoLuong,0)<0;
   IF @Negative=1 THROW 51207,N'Không đủ tồn kho. Ghi sổ sẽ làm tồn âm tại một thời điểm.',1;
  END;
  COMMIT;
  SET @SavedId=@Id;
  IF @ReturnResult=1 SELECT @Id AS ChungTuKhoId;
 END TRY
 BEGIN CATCH
  IF XACT_STATE()<>0 ROLLBACK;
  THROW;
 END CATCH;
END;
GO
CREATE OR ALTER PROCEDURE dbo.DeleteStockDraft
 @Id bigint,@Version binary(8),@Unit varchar(20),@Kind char(1)
AS
BEGIN
 SET NOCOUNT ON;
 SET XACT_ABORT ON;
 BEGIN TRY
  BEGIN TRANSACTION;
  DECLARE @Found varchar(20);
  SELECT @Found=MaDonVi FROM dbo.DonVi WITH(XLOCK,HOLDLOCK) WHERE MaDonVi=@Unit;
  IF @Found IS NULL THROW 51204,N'Đơn vị không tồn tại.',1;
  IF NOT EXISTS(SELECT 1 FROM dbo.ChungTuKho WITH(UPDLOCK,HOLDLOCK)
    WHERE ChungTuKhoId=@Id AND MaDonVi=@Unit AND Loai=@Kind AND PhienBan=@Version
     AND TrangThai='DRAFT' AND ChungTuThuongMaiId IS NULL)
   THROW 51206,N'Chỉ xóa được phiếu nháp chưa thay đổi.',1;
  DELETE dbo.CTChungTuKho WHERE ChungTuKhoId=@Id;
  DELETE dbo.ChungTuKho WHERE ChungTuKhoId=@Id;
  COMMIT;
 END TRY
 BEGIN CATCH
  IF XACT_STATE()<>0 ROLLBACK;
  THROW;
 END CATCH;
END;
GO
CREATE OR ALTER PROCEDURE dbo.InventoryReport
 @Unit varchar(20),@From date,@To date,@Item varchar(20)=NULL,@Mode char(1)='T'
AS
BEGIN
 SET NOCOUNT ON;
 IF @From IS NULL OR @To IS NULL OR @From>@To OR @Mode IS NULL OR @Mode NOT IN ('N','X','T')
  THROW 51208,N'Điều kiện báo cáo không hợp lệ.',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.DonVi WHERE MaDonVi=@Unit AND NgayBatDau<=@From)
  THROW 51204,N'Đơn vị không hợp lệ hoặc kỳ báo cáo trước ngày bắt đầu.',1;
 SET @Item=NULLIF(LTRIM(RTRIM(@Item)),'');
 IF @Item IS NOT NULL AND NOT EXISTS(SELECT 1 FROM dbo.HangHoa WHERE MaHang=@Item)
  THROW 51205,N'Mã hàng không tồn tại.',1;
 IF @Mode IN ('N','X')
 BEGIN
  SELECT h.Ngay,h.SoChungTu,d.MaHang,i.TenHang,i.DonViTinh,d.SoLuong,d.DonGia,d.ThanhTien
  FROM dbo.ChungTuKho h JOIN dbo.CTChungTuKho d ON d.ChungTuKhoId=h.ChungTuKhoId
   JOIN dbo.HangHoa i ON i.MaHang=d.MaHang
  WHERE h.MaDonVi=@Unit AND h.Loai=@Mode AND h.TrangThai='POSTED'
   AND h.Ngay>=@From AND h.Ngay<=@To AND (@Item IS NULL OR d.MaHang=@Item)
  ORDER BY h.Ngay,h.ChungTuKhoId,d.Dong;
 END
 ELSE
 BEGIN
  ;WITH Movements AS(
   SELECT d.MaHang,
    CONVERT(decimal(28,3),SUM(CASE WHEN h.Ngay<@From THEN CASE h.Loai WHEN 'N' THEN d.SoLuong ELSE -d.SoLuong END ELSE 0 END)) AS BeforePeriod,
    CONVERT(decimal(28,3),SUM(CASE WHEN h.Ngay>=@From AND h.Loai='N' THEN d.SoLuong ELSE 0 END)) AS Nhap,
    CONVERT(decimal(28,3),SUM(CASE WHEN h.Ngay>=@From AND h.Loai='X' THEN d.SoLuong ELSE 0 END)) AS Xuat
   FROM dbo.ChungTuKho h JOIN dbo.CTChungTuKho d ON d.ChungTuKhoId=h.ChungTuKhoId
   WHERE h.MaDonVi=@Unit AND h.TrangThai='POSTED' AND h.Ngay<=@To
    AND (@Item IS NULL OR d.MaHang=@Item)
   GROUP BY d.MaHang
  )
  SELECT i.MaHang,i.TenHang,i.DonViTinh,
   COALESCE(o.SoLuong,0)+COALESCE(m.BeforePeriod,0) AS TonDau,
   COALESCE(m.Nhap,0) AS Nhap,COALESCE(m.Xuat,0) AS Xuat,
   COALESCE(o.SoLuong,0)+COALESCE(m.BeforePeriod,0)+COALESCE(m.Nhap,0)-COALESCE(m.Xuat,0) AS TonCuoi
  FROM dbo.HangHoa i
   LEFT JOIN dbo.TonDauKy o ON o.MaHang=i.MaHang AND o.MaDonVi=@Unit
   LEFT JOIN Movements m ON m.MaHang=i.MaHang
  WHERE (@Item IS NULL OR i.MaHang=@Item)
  ORDER BY i.MaHang;
 END;
END;
GO
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaVersion WHERE Version=2) INSERT dbo.SchemaVersion(Version) VALUES(2);
COMMIT;
PRINT 'Phase 4 inventory procedures ready (schema version 2).';



