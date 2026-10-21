:On Error exit
USE ReportingModuleDemo;
GO
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
SET XACT_ABORT ON;
IF (SELECT COUNT(*) FROM dbo.SchemaVersion WHERE Version=1)<>1 THROW 51100,'Missing schema version.',1;
IF (SELECT COUNT(*) FROM dbo.SeedHistory WHERE SeedName='phase2-demo-v1')<>1 THROW 51101,'Missing seed marker.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.TonDauKy WHERE MaDonVi='DVDEMO' AND MaHang='HANG001' AND SoLuong=100) THROW 51102,'Opening 100 missing.',1;
IF (SELECT COUNT(*) FROM dbo.HangHoa WHERE MaHang LIKE 'HANG00%' OR MaHang='HANG010')<10 THROW 51103,'Item seed missing.',1;
IF (SELECT COUNT(*) FROM dbo.KhachHang WHERE MaKH LIKE 'KHDEMO%')<5 THROW 51104,'Customer seed missing.',1;
IF (SELECT COUNT(*) FROM dbo.NhaCungCap WHERE MaNCC LIKE 'NCCDEMO%')<5 THROW 51105,'Supplier seed missing.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.DonVi WHERE MaDonVi='DVDEMO' AND TenDonVi=N'Công ty TNHH PicoCorp') THROW 51108,'Demo unit display name is not updated.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.KhachHang WHERE MaKH='KHDEMO001' AND TenKH=N'Công ty TNHH Minh Phát') THROW 51109,'Demo customer names are not updated.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.NhaCungCap WHERE MaNCC='NCCDEMO001' AND TenNCC=N'Công ty TNHH Vật tư Hòa Bình') THROW 51110,'Demo supplier names are not updated.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.HangHoa WHERE MaHang='HANG001' AND TenHang=N'Thùng carton 5 lớp 40 x 30 x 30 cm' AND DonViTinh=N'Cái') THROW 51111,'Demo item names are not updated.',1;
IF EXISTS(SELECT 1 FROM dbo.TaiKhoan WHERE MaTK='TS_KH' AND TenTK=N'Phải thu') THROW 51112,'Teaching account display name is not updated.',1;
IF EXISTS(SELECT 1 FROM sys.foreign_keys WHERE is_disabled=1 OR is_not_trusted=1) THROW 51106,'Untrusted FK.',1;
IF EXISTS(SELECT 1 FROM sys.check_constraints WHERE is_disabled=1 OR is_not_trusted=1) THROW 51107,'Untrusted check constraint.',1;
PRINT 'PASS: version, seed counts, HANG001 opening 100, trusted constraints.';

-- All positive and negative fixtures are rolled back, leaving seed unchanged.
BEGIN TRANSACTION;
INSERT dbo.ChungTuKho(MaDonVi,Loai,SoChungTu,Ngay) VALUES('DVDEMO','N','VERIFY-ONLY','20260102');
DECLARE @id bigint=SCOPE_IDENTITY();
INSERT dbo.CTChungTuKho(ChungTuKhoId,Dong,MaHang,SoLuong,DonGia)
 VALUES(@id,1,'HANG001',1.125,2.3456),(@id,2,'HANG001',2,0);
IF (SELECT ThanhTien FROM dbo.CTChungTuKho WHERE ChungTuKhoId=@id AND Dong=1)<>2.6388
 THROW 51108,'Computed line rounding incorrect.',1;
IF (SELECT SUM(ThanhTien) FROM dbo.CTChungTuKho WHERE ChungTuKhoId=@id)<>2.6388
 THROW 51109,'Derived total incorrect.',1;
ROLLBACK;
PRINT 'PASS: decimal line amount, zero price and derived document total; fixture rolled back.';

DECLARE @Cases table(Name varchar(80), Statement nvarchar(max), ExpectedError int);
INSERT @Cases VALUES
('Duplicate item',N'INSERT dbo.HangHoa(MaHang,TenHang,DonViTinh) VALUES(''HANG001'',N''test'',N''test'')',2627),
('Blank name',N'INSERT dbo.HangHoa(MaHang,TenHang,DonViTinh) VALUES(''VERIFY'',N''  '',N''test'')',547),
('Negative price',N'INSERT dbo.HangHoa(MaHang,TenHang,DonViTinh,GiaNhap) VALUES(''VERIFY'',N''test'',N''test'',-1)',547),
('Negative opening',N'UPDATE dbo.TonDauKy SET SoLuong=-1 WHERE MaDonVi=''DVDEMO'' AND MaHang=''HANG001''',547),
('Unknown item FK',N'INSERT dbo.TonDauKy(MaDonVi,MaHang,SoLuong) VALUES(''DVDEMO'',''NO-SUCH-ITEM'',0)',547),
('Referenced item delete',N'DELETE dbo.HangHoa WHERE MaHang=''HANG001''',547),
('Wrong invoice partner',N'INSERT dbo.ChungTuThuongMai(MaDonVi,Loai,SoChungTu,Ngay,MaNCC) VALUES(''DVDEMO'',''SALES_INVOICE'',''VERIFY'',''20260102'',''NCCDEMO001'')',547),
('Invalid status',N'INSERT dbo.ChungTuKho(MaDonVi,Loai,SoChungTu,Ngay,TrangThai) VALUES(''DVDEMO'',''N'',''VERIFY'',''20260102'',''INVALID'')',547),
('Zero quantity',N'INSERT dbo.ChungTuKho(MaDonVi,Loai,SoChungTu,Ngay) VALUES(''DVDEMO'',''N'',''VERIFY'',''20260102''); DECLARE @d bigint=SCOPE_IDENTITY(); INSERT dbo.CTChungTuKho VALUES(@d,1,''HANG001'',0,1)',547),
('Negative quantity',N'INSERT dbo.ChungTuKho(MaDonVi,Loai,SoChungTu,Ngay) VALUES(''DVDEMO'',''N'',''VERIFY'',''20260102''); DECLARE @d bigint=SCOPE_IDENTITY(); INSERT dbo.CTChungTuKho VALUES(@d,1,''HANG001'',-1,1)',547),
('Negative detail price',N'INSERT dbo.ChungTuKho(MaDonVi,Loai,SoChungTu,Ngay) VALUES(''DVDEMO'',''N'',''VERIFY'',''20260102''); DECLARE @d bigint=SCOPE_IDENTITY(); INSERT dbo.CTChungTuKho VALUES(@d,1,''HANG001'',1,-1)',547),
('Unbalanced journal line shape',N'INSERT dbo.TaiKhoan VALUES(''VERIFY'',N''test''); INSERT dbo.PhieuKeToan(MaDonVi,SoPhieu,Ngay) VALUES(''DVDEMO'',''VERIFY'',''20260102''); DECLARE @d bigint=SCOPE_IDENTITY(); INSERT dbo.CTPhieuKeToan VALUES(@d,1,''VERIFY'',10,10)',547);
DECLARE @name varchar(80), @statement nvarchar(max), @expected int, @actual int;
DECLARE checks CURSOR LOCAL FAST_FORWARD FOR SELECT Name,Statement,ExpectedError FROM @Cases;
OPEN checks;
FETCH NEXT FROM checks INTO @name,@statement,@expected;
WHILE @@FETCH_STATUS=0
BEGIN
 SET @actual=0;
 BEGIN TRY
  BEGIN TRANSACTION;
  EXEC sys.sp_executesql @statement;
  ROLLBACK;
 END TRY
 BEGIN CATCH
  SET @actual=ERROR_NUMBER();
  IF XACT_STATE()<>0 ROLLBACK;
 END CATCH;
 IF @actual<>@expected
 BEGIN
  PRINT @name;
  THROW 51110,'Constraint test did not fail as expected.',1;
 END;
 PRINT 'PASS: '+@name;
 FETCH NEXT FROM checks INTO @name,@statement,@expected;
END;
CLOSE checks;
DEALLOCATE checks;
IF EXISTS(SELECT 1 FROM dbo.ChungTuKho WHERE SoChungTu IN ('VERIFY','VERIFY-ONLY')) THROW 51111,'Test header survived rollback.',1;
DBCC CHECKCONSTRAINTS WITH ALL_CONSTRAINTS;
PRINT 'PASS: verification complete; no acceptance posting or stock rejection claimed in Phase 2.';

