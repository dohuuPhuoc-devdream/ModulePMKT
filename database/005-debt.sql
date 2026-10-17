SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
BEGIN TRANSACTION;
DECLARE @Lock int;
EXEC @Lock=sys.sp_getapplock @Resource=N'ReportingModule.Schema',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;
IF @Lock<0 THROW 51400,'Cannot acquire migration lock.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaVersion WHERE Version=3) THROW 51400,'Commerce schema required.',1;
IF OBJECT_ID('dbo.ThanhToanHoaDon','U') IS NULL
BEGIN
 CREATE TABLE dbo.ThanhToanHoaDon(
  ThanhToanId bigint IDENTITY PRIMARY KEY,
  ChungTuId bigint NOT NULL REFERENCES dbo.ChungTuThuongMai(ChungTuId),
  SoThanhToan varchar(30) NOT NULL,
  Ngay date NOT NULL,
  SoTien decimal(28,4) NOT NULL CHECK(SoTien>0),
  DienGiai nvarchar(500) NULL,
  RequestId uniqueidentifier NOT NULL UNIQUE,
  CONSTRAINT UQ_ThanhToan_Number UNIQUE(ChungTuId,SoThanhToan),
  CONSTRAINT CK_ThanhToan_Number CHECK(LEN(LTRIM(RTRIM(SoThanhToan)))>0)
 );
 CREATE INDEX IX_ThanhToan_Date ON dbo.ThanhToanHoaDon(ChungTuId,Ngay) INCLUDE(SoTien);
END;
GO
CREATE OR ALTER PROCEDURE dbo.RecordInvoicePayment
 @Id bigint,@Unit varchar(20),@Kind varchar(20),@Number varchar(30),@Date date,
 @Amount decimal(28,4),@Note nvarchar(500)=NULL,@RequestId uniqueidentifier
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 IF @Kind IS NULL OR @Kind NOT IN ('SALES_INVOICE','PURCHASE_INVOICE') OR @Date IS NULL
  OR @Amount IS NULL OR @Amount<=0 OR @RequestId IS NULL OR NULLIF(LTRIM(RTRIM(@Number)),'') IS NULL
  THROW 51401,N'Thông tin thanh toán không hợp lệ.',1;
 BEGIN TRY
  BEGIN TRANSACTION;
  DECLARE @Found varchar(20),@InvoiceDate date;
  SELECT @Found=MaDonVi FROM dbo.DonVi WITH(XLOCK,HOLDLOCK) WHERE MaDonVi=@Unit;
  SELECT @InvoiceDate=Ngay FROM dbo.ChungTuThuongMai WHERE ChungTuId=@Id AND MaDonVi=@Unit AND Loai=@Kind AND TrangThai='POSTED';
  IF @Found IS NULL OR @InvoiceDate IS NULL OR @Date<@InvoiceDate
   THROW 51402,N'Chỉ thanh toán hóa đơn đã ghi sổ, từ ngày hóa đơn trở đi.',1;
  IF EXISTS(SELECT 1 FROM dbo.ThanhToanHoaDon WHERE RequestId=@RequestId)
  BEGIN
   IF NOT EXISTS(SELECT 1 FROM dbo.ThanhToanHoaDon WHERE RequestId=@RequestId AND ChungTuId=@Id AND SoThanhToan=@Number AND Ngay=@Date AND SoTien=@Amount AND ISNULL(DienGiai,N'')=ISNULL(@Note,N''))
    THROW 51403,N'Yêu cầu đã được sử dụng. Hãy mở lại hóa đơn.',1;
   COMMIT; RETURN;
  END;
  DECLARE @Total decimal(32,4),@Paid decimal(32,4);
  SELECT @Total=SUM(ThanhTien) FROM dbo.CTChungTuThuongMai WHERE ChungTuId=@Id;
  SELECT @Paid=COALESCE(SUM(SoTien),0) FROM dbo.ThanhToanHoaDon WHERE ChungTuId=@Id;
  IF @Total IS NULL OR @Amount>@Total-@Paid THROW 51404,N'Số tiền vượt phần còn phải thanh toán của hóa đơn.',1;
  INSERT dbo.ThanhToanHoaDon(ChungTuId,SoThanhToan,Ngay,SoTien,DienGiai,RequestId)
   VALUES(@Id,LTRIM(RTRIM(@Number)),@Date,@Amount,@Note,@RequestId);
  COMMIT;
 END TRY
 BEGIN CATCH
  IF XACT_STATE()<>0 ROLLBACK;
  THROW;
 END CATCH;
END;
GO
CREATE OR ALTER PROCEDURE dbo.DebtReport @Unit varchar(20),@From date,@To date,@Partner varchar(20)=NULL,@Mode varchar(20)
AS
BEGIN
 SET NOCOUNT ON;
 IF @From IS NULL OR @To IS NULL OR @From>@To OR @Mode IS NULL OR @Mode NOT IN ('receivables','payables')
  THROW 51405,N'Điều kiện công nợ không hợp lệ.',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.DonVi WHERE MaDonVi=@Unit AND NgayBatDau<=@From)
  THROW 51405,N'Đơn vị hoặc kỳ công nợ không hợp lệ.',1;
 SET @Partner=NULLIF(LTRIM(RTRIM(@Partner)),'');
 IF @Partner IS NOT NULL AND ((@Mode='receivables' AND NOT EXISTS(SELECT 1 FROM dbo.KhachHang WHERE MaKH=@Partner)) OR (@Mode='payables' AND NOT EXISTS(SELECT 1 FROM dbo.NhaCungCap WHERE MaNCC=@Partner)))
  THROW 51405,N'Mã khách hàng/nhà cung cấp không tồn tại.',1;
 ;WITH Invoices AS(
  SELECT h.ChungTuId,h.Ngay,COALESCE(h.MaKH,h.MaNCC) AS MaDoiTac,CONVERT(decimal(32,4),SUM(d.ThanhTien)) AS Amount
  FROM dbo.ChungTuThuongMai h JOIN dbo.CTChungTuThuongMai d ON d.ChungTuId=h.ChungTuId
  WHERE h.MaDonVi=@Unit AND h.Loai=CASE WHEN @Mode='receivables' THEN 'SALES_INVOICE' ELSE 'PURCHASE_INVOICE' END
   AND h.TrangThai='POSTED' AND h.Ngay<=@To AND (@Partner IS NULL OR COALESCE(h.MaKH,h.MaNCC)=@Partner)
  GROUP BY h.ChungTuId,h.Ngay,h.MaKH,h.MaNCC
 ), Events AS(
  SELECT MaDoiTac,Ngay,Amount AS Increase,CONVERT(decimal(32,4),0) AS Decrease FROM Invoices
  UNION ALL
  SELECT i.MaDoiTac,p.Ngay,0,p.SoTien FROM Invoices i JOIN dbo.ThanhToanHoaDon p ON p.ChungTuId=i.ChungTuId WHERE p.Ngay<=@To
 ), Totals AS(
  SELECT MaDoiTac,
   CONVERT(decimal(32,4),SUM(CASE WHEN Ngay<@From THEN Increase-Decrease ELSE 0 END)) AS DuDau,
   CONVERT(decimal(32,4),SUM(CASE WHEN Ngay>=@From THEN Increase ELSE 0 END)) AS PhatSinh,
   CONVERT(decimal(32,4),SUM(CASE WHEN Ngay>=@From THEN Decrease ELSE 0 END)) AS ThanhToan
  FROM Events GROUP BY MaDoiTac
 )
 SELECT t.MaDoiTac,COALESCE(k.TenKH,n.TenNCC) AS TenDoiTac,t.DuDau,t.PhatSinh,t.ThanhToan,t.DuDau+t.PhatSinh-t.ThanhToan AS DuCuoi
 FROM Totals t LEFT JOIN dbo.KhachHang k ON @Mode='receivables' AND k.MaKH=t.MaDoiTac
 LEFT JOIN dbo.NhaCungCap n ON @Mode='payables' AND n.MaNCC=t.MaDoiTac
 ORDER BY t.MaDoiTac;
END;
GO
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaVersion WHERE Version=4) INSERT dbo.SchemaVersion(Version) VALUES(4);
COMMIT;
PRINT 'Phase 6 debt/payment procedures ready (schema version 4).';
