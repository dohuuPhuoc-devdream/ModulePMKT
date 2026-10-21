SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
BEGIN TRANSACTION;
DECLARE @Lock int;
EXEC @Lock=sys.sp_getapplock @Resource=N'ReportingModule.Schema',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;
IF @Lock<0 THROW 52100,'Cannot acquire migration lock.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaVersion WHERE Version=10) THROW 52101,'Schema version 10 required.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaVersion WHERE Version=11)
BEGIN
 UPDATE dbo.DonVi
 SET TenDonVi=N'Công ty TNHH PicoCorp',
     TenPhapLy=CASE WHEN TenPhapLy IS NULL OR TenPhapLy=N'Đơn vị thực hành (dữ liệu giả lập)' THEN N'Công ty TNHH PicoCorp' ELSE TenPhapLy END,
     DiaChi=COALESCE(NULLIF(DiaChi,N''),N'Số 01, Đường Mẫu, Phường An Khánh, Thành phố Hồ Chí Minh')
 WHERE MaDonVi='DVDEMO' AND (TenDonVi=N'Đơn vị thực hành (dữ liệu giả lập)' OR TenDonVi=N'Công ty TNHH PicoCorp');

 UPDATE kh SET TenKH=m.Ten
 FROM dbo.KhachHang kh JOIN (VALUES
  ('KHDEMO001',N'Công ty TNHH Minh Phát'),('KHDEMO002',N'Công ty Cổ phần An Khang'),
  ('KHDEMO003',N'Công ty TNHH Thiết bị Thành Công'),('KHDEMO004',N'Công ty TNHH Thương mại Phú An'),
  ('KHDEMO005',N'Công ty Cổ phần Cơ điện Nam Việt')
 ) m(Ma,Ten) ON m.Ma=kh.MaKH
 WHERE kh.TenKH=N'Khách hàng thực hành '+RIGHT(kh.MaKH,1);

 UPDATE ncc SET TenNCC=m.Ten
 FROM dbo.NhaCungCap ncc JOIN (VALUES
  ('NCCDEMO001',N'Công ty TNHH Vật tư Hòa Bình'),('NCCDEMO002',N'Công ty Cổ phần Kim khí Đại Việt'),
  ('NCCDEMO003',N'Công ty TNHH Thiết bị Kỹ thuật Đông Á'),('NCCDEMO004',N'Công ty TNHH Thương mại Gia Phúc'),
  ('NCCDEMO005',N'Công ty Cổ phần Công nghiệp Tân Tiến')
 ) m(Ma,Ten) ON m.Ma=ncc.MaNCC
 WHERE ncc.TenNCC=N'Nhà cung cấp thực hành '+RIGHT(ncc.MaNCC,1);

 UPDATE h SET TenHang=m.Ten,DonViTinh=m.Dvt
 FROM dbo.HangHoa h JOIN (VALUES
  ('HANG001',N'Thùng carton 5 lớp 40 x 30 x 30 cm',N'Cái'),('HANG002',N'Dây điện CV 2.5 mm²',N'Mét'),
  ('HANG003',N'Ống thép mạ kẽm DN20',N'Cây'),('HANG004',N'Bu lông inox M12 x 40',N'Cái'),
  ('HANG005',N'Sơn chống gỉ màu xám 18 lít',N'Thùng'),('HANG006',N'Găng tay bảo hộ phủ cao su',N'Đôi'),
  ('HANG007',N'Băng keo đóng gói 48 mm x 100 m',N'Cuộn'),('HANG008',N'Vòng bi 6204-2RS',N'Cái'),
  ('HANG009',N'Dầu nhớt công nghiệp ISO VG 68',N'Lít'),('HANG010',N'Khẩu trang lọc bụi than hoạt tính',N'Cái')
 ) m(Ma,Ten,Dvt) ON m.Ma=h.MaHang
 WHERE h.TenHang=N'Hàng thực hành '+CONVERT(nvarchar(3),TRY_CONVERT(int,RIGHT(h.MaHang,3)));

 UPDATE t SET TenTK=m.Ten
 FROM dbo.TaiKhoan t JOIN (VALUES
  ('TS_TIEN',N'Tiền mặt và tiền gửi ngân hàng',N'Tiền'),('TS_KH',N'Phải thu khách hàng',N'Phải thu'),
  ('TS_HANG',N'Hàng tồn kho',N'Hàng tồn kho'),('NV_NCC',N'Phải trả nhà cung cấp',N'Phải trả'),
  ('NV_VON',N'Vốn góp của chủ sở hữu',N'Vốn góp'),('NV_LN',N'Lợi nhuận sau thuế chưa phân phối',N'Kết quả tích lũy')
 ) m(Ma,Ten,TenCu) ON m.Ma=t.MaTK
 WHERE t.TenTK=m.TenCu;

 INSERT dbo.SchemaVersion(Version) VALUES(11);
END;
COMMIT;
PRINT 'Schema version 11 ready (natural synthetic demo display names).';
GO
