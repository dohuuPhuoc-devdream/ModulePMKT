SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
SET XACT_ABORT ON;
BEGIN TRANSACTION;
DECLARE @LockResult int;
EXEC @LockResult=sys.sp_getapplock @Resource=N'ReportingModule.Schema',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;
IF @LockResult<0 THROW 51002,'Cannot acquire seed lock.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaVersion WHERE Version=1) THROW 51003,'Schema version 1 required.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.SeedHistory WHERE SeedName='phase2-demo-v1')
BEGIN
-- Reserved identifiers: fail atomically if existing user records collide; never overwrite.
INSERT dbo.DonVi(MaDonVi,TenDonVi,NgayBatDau) VALUES('DVDEMO',N'Đơn vị thực hành (dữ liệu giả lập)','20260101');
DECLARE @i int=1, @suffix varchar(3);
WHILE @i<=10
BEGIN
 SET @suffix=RIGHT('000'+CONVERT(varchar(3),@i),3);
 INSERT dbo.HangHoa(MaHang,TenHang,DonViTinh,GiaNhap,GiaBan)
 VALUES('HANG'+@suffix,N'Hàng thực hành '+CONVERT(nvarchar(3),@i),N'Cái',10000+@i*1000,15000+@i*1000);
 INSERT dbo.TonDauKy(MaDonVi,MaHang,SoLuong) VALUES('DVDEMO','HANG'+@suffix,CASE WHEN @i=1 THEN 100 ELSE 0 END);
 SET @i+=1;
END;
SET @i=1;
WHILE @i<=5
BEGIN
 SET @suffix=RIGHT('000'+CONVERT(varchar(3),@i),3);
 INSERT dbo.KhachHang(MaKH,TenKH) VALUES('KHDEMO'+@suffix,N'Khách hàng thực hành '+CONVERT(nvarchar(3),@i));
 INSERT dbo.NhaCungCap(MaNCC,TenNCC) VALUES('NCCDEMO'+@suffix,N'Nhà cung cấp thực hành '+CONVERT(nvarchar(3),@i));
 SET @i+=1;
END;
INSERT dbo.SeedHistory(SeedName) VALUES('phase2-demo-v1');
END;
COMMIT;
PRINT 'Synthetic phase 2 seed ready; no transaction documents seeded.';
