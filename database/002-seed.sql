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
INSERT dbo.DonVi(MaDonVi,TenDonVi,NgayBatDau) VALUES('DVDEMO',N'Công ty TNHH PicoCorp','20260101');
DECLARE @i int=1, @suffix varchar(3);
WHILE @i<=10
BEGIN
 SET @suffix=RIGHT('000'+CONVERT(varchar(3),@i),3);
 INSERT dbo.HangHoa(MaHang,TenHang,DonViTinh,GiaNhap,GiaBan)
 VALUES('HANG'+@suffix,
  CASE @i WHEN 1 THEN N'Thùng carton 5 lớp 40 x 30 x 30 cm' WHEN 2 THEN N'Dây điện CV 2.5 mm²' WHEN 3 THEN N'Ống thép mạ kẽm DN20' WHEN 4 THEN N'Bu lông inox M12 x 40' WHEN 5 THEN N'Sơn chống gỉ màu xám 18 lít' WHEN 6 THEN N'Găng tay bảo hộ phủ cao su' WHEN 7 THEN N'Băng keo đóng gói 48 mm x 100 m' WHEN 8 THEN N'Vòng bi 6204-2RS' WHEN 9 THEN N'Dầu nhớt công nghiệp ISO VG 68' ELSE N'Khẩu trang lọc bụi than hoạt tính' END,
  CASE @i WHEN 2 THEN N'Mét' WHEN 3 THEN N'Cây' WHEN 5 THEN N'Thùng' WHEN 6 THEN N'Đôi' WHEN 7 THEN N'Cuộn' WHEN 9 THEN N'Lít' ELSE N'Cái' END,
 10000+@i*1000,15000+@i*1000);
 INSERT dbo.TonDauKy(MaDonVi,MaHang,SoLuong) VALUES('DVDEMO','HANG'+@suffix,CASE WHEN @i=1 THEN 100 ELSE 0 END);
 SET @i+=1;
END;
INSERT dbo.KhachHang(MaKH,TenKH) VALUES
 ('KHDEMO001',N'Công ty TNHH Minh Phát'),('KHDEMO002',N'Công ty Cổ phần An Khang'),
 ('KHDEMO003',N'Công ty TNHH Thiết bị Thành Công'),('KHDEMO004',N'Công ty TNHH Thương mại Phú An'),
 ('KHDEMO005',N'Công ty Cổ phần Cơ điện Nam Việt');
INSERT dbo.NhaCungCap(MaNCC,TenNCC) VALUES
 ('NCCDEMO001',N'Công ty TNHH Vật tư Hòa Bình'),('NCCDEMO002',N'Công ty Cổ phần Kim khí Đại Việt'),
 ('NCCDEMO003',N'Công ty TNHH Thiết bị Kỹ thuật Đông Á'),('NCCDEMO004',N'Công ty TNHH Thương mại Gia Phúc'),
 ('NCCDEMO005',N'Công ty Cổ phần Công nghiệp Tân Tiến');
INSERT dbo.SeedHistory(SeedName) VALUES('phase2-demo-v1');
END;
COMMIT;
PRINT 'Synthetic phase 2 seed ready; no transaction documents seeded.';
