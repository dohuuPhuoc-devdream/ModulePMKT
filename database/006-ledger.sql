SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
BEGIN TRANSACTION;
DECLARE @Lock int;
EXEC @Lock=sys.sp_getapplock @Resource=N'ReportingModule.Schema',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;
IF @Lock<0 THROW 51500,'Cannot acquire migration lock.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaVersion WHERE Version=4) THROW 51500,'Schema 4 required.',1;
IF OBJECT_ID('dbo.TaiKhoanBaoCao','U') IS NULL
 CREATE TABLE dbo.TaiKhoanBaoCao(MaTK varchar(20) PRIMARY KEY REFERENCES dbo.TaiKhoan(MaTK),Nhom char(1) NOT NULL CHECK(Nhom IN ('A','L','E')),ThuTu int NOT NULL UNIQUE);
-- Internal teaching chart, not a statutory chart or FAST format. No company balances.
DECLARE @Accounts TABLE(Ma varchar(20),Ten nvarchar(200),TenCu nvarchar(200),Nhom char(1),ThuTu int);
INSERT @Accounts VALUES
 ('TS_TIEN',N'Tiền mặt và tiền gửi ngân hàng',N'Tiền','A',1),('TS_KH',N'Phải thu khách hàng',N'Phải thu','A',2),('TS_HANG',N'Hàng tồn kho',N'Hàng tồn kho','A',3),
 ('NV_NCC',N'Phải trả nhà cung cấp',N'Phải trả','L',101),('NV_VON',N'Vốn góp của chủ sở hữu',N'Vốn góp','E',201),('NV_LN',N'Lợi nhuận sau thuế chưa phân phối',N'Kết quả tích lũy','E',202);
IF EXISTS(SELECT 1 FROM @Accounts a JOIN dbo.TaiKhoan t ON t.MaTK=a.Ma WHERE t.TenTK NOT IN(a.Ten,a.TenCu))
 THROW 51500,'Existing teaching account code has a different name; no overwrite.',1;
INSERT dbo.TaiKhoan(MaTK,TenTK) SELECT Ma,Ten FROM @Accounts a WHERE NOT EXISTS(SELECT 1 FROM dbo.TaiKhoan t WHERE t.MaTK=a.Ma);
IF EXISTS(SELECT 1 FROM @Accounts a JOIN dbo.TaiKhoanBaoCao m ON m.MaTK=a.Ma WHERE m.Nhom<>a.Nhom OR m.ThuTu<>a.ThuTu)
 THROW 51500,'Existing teaching mapping differs; no overwrite.',1;
-- On a full setup rerun after the TT99 migration, the old teaching-account
-- report positions may already belong to their statutory replacements. Do
-- not recreate those obsolete mappings or violate the unique report order.
INSERT dbo.TaiKhoanBaoCao(MaTK,Nhom,ThuTu)
 SELECT a.Ma,a.Nhom,a.ThuTu FROM @Accounts a
 WHERE NOT EXISTS(SELECT 1 FROM dbo.TaiKhoanBaoCao m WHERE m.MaTK=a.Ma)
   AND NOT EXISTS(SELECT 1 FROM dbo.TaiKhoanBaoCao m WHERE m.ThuTu=a.ThuTu);
IF TYPE_ID('dbo.JournalLines') IS NULL EXEC('CREATE TYPE dbo.JournalLines AS TABLE(Dong int PRIMARY KEY,MaTK varchar(20),PhatSinhNo decimal(28,4),PhatSinhCo decimal(28,4))');
GO
CREATE OR ALTER PROCEDURE dbo.SaveJournal
 @Id bigint=NULL,@Version binary(8)=NULL,@Unit varchar(20),@Number varchar(30),@Date date,@Note nvarchar(500)=NULL,@Post bit=0,@Lines dbo.JournalLines READONLY
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 IF @Date IS NULL OR @Post IS NULL OR NULLIF(LTRIM(RTRIM(@Number)),'') IS NULL THROW 51501,N'Số phiếu/ngày không hợp lệ.',1;
 IF NOT EXISTS(SELECT 1 FROM @Lines) OR (SELECT COUNT(*) FROM @Lines)>100 OR EXISTS(SELECT 1 FROM @Lines WHERE Dong<=0 OR MaTK IS NULL OR PhatSinhNo IS NULL OR PhatSinhCo IS NULL OR NOT((PhatSinhNo>0 AND PhatSinhCo=0) OR (PhatSinhCo>0 AND PhatSinhNo=0)))
  THROW 51502,N'Mỗi dòng phải có một bên Nợ hoặc Có dương; tối đa 100 dòng.',1;
 IF @Post=1 AND (SELECT SUM(PhatSinhNo) FROM @Lines)<>(SELECT SUM(PhatSinhCo) FROM @Lines) THROW 51503,N'Tổng Nợ phải bằng tổng Có trước khi ghi sổ.',1;
 BEGIN TRY
  BEGIN TRANSACTION;
  DECLARE @Opening date;
  SELECT @Opening=NgayBatDau FROM dbo.DonVi WITH(XLOCK,HOLDLOCK) WHERE MaDonVi=@Unit;
  IF @Opening IS NULL OR @Date<@Opening THROW 51504,N'Đơn vị hoặc ngày phiếu không hợp lệ.',1;
  IF EXISTS(SELECT 1 FROM @Lines l LEFT JOIN dbo.TaiKhoanBaoCao a ON a.MaTK=l.MaTK WHERE a.MaTK IS NULL)
   THROW 51505,N'Tài khoản chưa có ánh xạ báo cáo thực hành.',1;
  IF @Id IS NULL
  BEGIN
   INSERT dbo.PhieuKeToan(MaDonVi,SoPhieu,Ngay,DienGiai) VALUES(@Unit,LTRIM(RTRIM(@Number)),@Date,@Note); SET @Id=SCOPE_IDENTITY();
  END
  ELSE
  BEGIN
   UPDATE dbo.PhieuKeToan SET SoPhieu=LTRIM(RTRIM(@Number)),Ngay=@Date,DienGiai=@Note WHERE PhieuId=@Id AND MaDonVi=@Unit AND PhienBan=@Version AND TrangThai='DRAFT';
   IF @@ROWCOUNT<>1 THROW 51506,N'Phiếu đã thay đổi hoặc đã ghi sổ. Hãy chọn lại.',1;
   DELETE dbo.CTPhieuKeToan WHERE PhieuId=@Id;
  END;
  INSERT dbo.CTPhieuKeToan(PhieuId,Dong,MaTK,PhatSinhNo,PhatSinhCo) SELECT @Id,Dong,MaTK,PhatSinhNo,PhatSinhCo FROM @Lines;
  IF @Post=1 UPDATE dbo.PhieuKeToan SET TrangThai='POSTED' WHERE PhieuId=@Id;
  COMMIT; SELECT @Id;
 END TRY
 BEGIN CATCH
  IF XACT_STATE()<>0 ROLLBACK; THROW;
 END CATCH;
END;
GO
CREATE OR ALTER PROCEDURE dbo.DeleteJournal @Id bigint,@Version binary(8),@Unit varchar(20)
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 BEGIN TRY
  BEGIN TRANSACTION;
  DECLARE @Found varchar(20); SELECT @Found=MaDonVi FROM dbo.DonVi WITH(XLOCK,HOLDLOCK) WHERE MaDonVi=@Unit;
  IF @Found IS NULL OR NOT EXISTS(SELECT 1 FROM dbo.PhieuKeToan WHERE PhieuId=@Id AND MaDonVi=@Unit AND PhienBan=@Version AND TrangThai='DRAFT') THROW 51506,N'Chỉ xóa được phiếu nháp chưa thay đổi.',1;
  DELETE dbo.CTPhieuKeToan WHERE PhieuId=@Id; DELETE dbo.PhieuKeToan WHERE PhieuId=@Id;
  COMMIT;
 END TRY
 BEGIN CATCH
  IF XACT_STATE()<>0 ROLLBACK; THROW;
 END CATCH;
END;
GO
CREATE OR ALTER PROCEDURE dbo.BalanceReport @Unit varchar(20),@From date,@To date
AS
BEGIN
 SET NOCOUNT ON;
 IF @From IS NULL OR @To IS NULL OR @From>@To OR NOT EXISTS(SELECT 1 FROM dbo.DonVi WHERE MaDonVi=@Unit AND NgayBatDau<=@From)
  THROW 51507,N'Đơn vị hoặc khoảng ngày không hợp lệ.',1;
 IF EXISTS(SELECT 1 FROM dbo.PhieuKeToan h JOIN dbo.CTPhieuKeToan d ON d.PhieuId=h.PhieuId LEFT JOIN dbo.TaiKhoanBaoCao m ON m.MaTK=d.MaTK WHERE h.MaDonVi=@Unit AND h.TrangThai='POSTED' AND h.Ngay<=@To AND m.MaTK IS NULL)
  THROW 51505,N'Có tài khoản đã ghi sổ chưa được ánh xạ; không thể lập báo cáo đầy đủ.',1;
 ;WITH Balances AS(
 SELECT d.MaTK,
  CONVERT(decimal(32,4),SUM(CASE WHEN h.Ngay<@From THEN d.PhatSinhNo-d.PhatSinhCo ELSE 0 END)) AS Opening,
  CONVERT(decimal(32,4),SUM(d.PhatSinhNo-d.PhatSinhCo)) AS Closing
 FROM dbo.PhieuKeToan h JOIN dbo.CTPhieuKeToan d ON d.PhieuId=h.PhieuId
 WHERE h.MaDonVi=@Unit AND h.TrangThai='POSTED' AND h.Ngay<=@To GROUP BY d.MaTK
 )
 SELECT m.MaTK,t.TenTK,m.Nhom,m.ThuTu,
  CONVERT(decimal(32,4),COALESCE(b.Opening,0)*CASE WHEN m.Nhom='A' THEN 1 ELSE -1 END) AS DauKy,
  CONVERT(decimal(32,4),COALESCE(b.Closing,0)*CASE WHEN m.Nhom='A' THEN 1 ELSE -1 END) AS CuoiKy
 INTO #Accounts FROM dbo.TaiKhoanBaoCao m JOIN dbo.TaiKhoan t ON t.MaTK=m.MaTK LEFT JOIN Balances b ON b.MaTK=m.MaTK;
 ;WITH Rows AS(
  SELECT Nhom AS Ma,CONVERT(nvarchar(200),CASE Nhom WHEN 'A' THEN N'TÀI SẢN' WHEN 'L' THEN N'NỢ PHẢI TRẢ' ELSE N'VỐN CHỦ SỞ HỮU' END) AS ChiTieu,
   0 AS Cap,CASE Nhom WHEN 'A' THEN 0 WHEN 'L' THEN 1000 ELSE 2000 END AS Sort,SUM(DauKy) AS DauKy,SUM(CuoiKy) AS CuoiKy FROM #Accounts GROUP BY Nhom
  UNION ALL SELECT MaTK,TenTK,1,CASE Nhom WHEN 'A' THEN 0 WHEN 'L' THEN 1000 ELSE 2000 END+ThuTu,DauKy,CuoiKy FROM #Accounts
  UNION ALL SELECT 'SOURCE',N'TỔNG NGUỒN VỐN',0,3000,COALESCE(SUM(DauKy),0),COALESCE(SUM(CuoiKy),0) FROM #Accounts WHERE Nhom IN ('L','E')
  UNION ALL SELECT 'CHECK',N'CHÊNH LỆCH TÀI SẢN - NGUỒN VỐN',0,4000,COALESCE(SUM(CASE Nhom WHEN 'A' THEN DauKy ELSE -DauKy END),0),COALESCE(SUM(CASE Nhom WHEN 'A' THEN CuoiKy ELSE -CuoiKy END),0) FROM #Accounts
 ) SELECT Ma,ChiTieu,Cap,DauKy,CuoiKy FROM Rows ORDER BY Sort;
END;
GO
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaVersion WHERE Version=5) INSERT dbo.SchemaVersion(Version) VALUES(5);
COMMIT;
PRINT 'Phase 7 journal/balance procedures ready (schema version 5).';

