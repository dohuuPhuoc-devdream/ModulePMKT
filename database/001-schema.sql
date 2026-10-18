SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
SET XACT_ABORT ON;
BEGIN TRANSACTION;
DECLARE @LockResult int;
EXEC @LockResult = sys.sp_getapplock @Resource=N'ReportingModule.Schema', @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000;
IF @LockResult < 0 THROW 51000, 'Cannot acquire schema lock.', 1;
IF OBJECT_ID(N'dbo.SchemaVersion', N'U') IS NULL
 CREATE TABLE dbo.SchemaVersion(Version int NOT NULL CONSTRAINT PK_SchemaVersion PRIMARY KEY, AppliedAt datetime2(0) NOT NULL CONSTRAINT DF_SchemaVersion_AppliedAt DEFAULT SYSUTCDATETIME());
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaVersion WHERE Version=1)
BEGIN
CREATE TABLE dbo.DonVi(
 MaDonVi varchar(20) NOT NULL CONSTRAINT PK_DonVi PRIMARY KEY,
 TenDonVi nvarchar(200) NOT NULL,
 NgayBatDau date NOT NULL,
 CONSTRAINT CK_DonVi_Required CHECK(LEN(LTRIM(RTRIM(MaDonVi)))>0 AND LEN(LTRIM(RTRIM(TenDonVi)))>0)
);
CREATE TABLE dbo.KhachHang(
 MaKH varchar(20) NOT NULL CONSTRAINT PK_KhachHang PRIMARY KEY,
 TenKH nvarchar(200) NOT NULL,
 MaSoThue varchar(20) NULL, DiaChi nvarchar(300) NULL, DienThoai varchar(30) NULL,
 CONSTRAINT CK_KhachHang_Required CHECK(LEN(LTRIM(RTRIM(MaKH)))>0 AND LEN(LTRIM(RTRIM(TenKH)))>0)
);
CREATE TABLE dbo.NhaCungCap(
 MaNCC varchar(20) NOT NULL CONSTRAINT PK_NhaCungCap PRIMARY KEY,
 TenNCC nvarchar(200) NOT NULL,
 MaSoThue varchar(20) NULL, DiaChi nvarchar(300) NULL, DienThoai varchar(30) NULL,
 CONSTRAINT CK_NhaCungCap_Required CHECK(LEN(LTRIM(RTRIM(MaNCC)))>0 AND LEN(LTRIM(RTRIM(TenNCC)))>0)
);
CREATE TABLE dbo.HangHoa(
 MaHang varchar(20) NOT NULL CONSTRAINT PK_HangHoa PRIMARY KEY,
 TenHang nvarchar(200) NOT NULL, DonViTinh nvarchar(30) NOT NULL,
 GiaNhap decimal(19,4) NOT NULL CONSTRAINT DF_HangHoa_GiaNhap DEFAULT 0,
 GiaBan decimal(19,4) NOT NULL CONSTRAINT DF_HangHoa_GiaBan DEFAULT 0,
 CONSTRAINT CK_HangHoa_Required CHECK(LEN(LTRIM(RTRIM(MaHang)))>0 AND LEN(LTRIM(RTRIM(TenHang)))>0 AND LEN(LTRIM(RTRIM(DonViTinh)))>0),
 CONSTRAINT CK_HangHoa_Prices CHECK(GiaNhap>=0 AND GiaBan>=0)
);
CREATE TABLE dbo.TonDauKy(
 MaDonVi varchar(20) NOT NULL CONSTRAINT FK_TonDauKy_DonVi REFERENCES dbo.DonVi(MaDonVi),
 MaHang varchar(20) NOT NULL CONSTRAINT FK_TonDauKy_HangHoa REFERENCES dbo.HangHoa(MaHang),
 SoLuong decimal(18,3) NOT NULL,
 CONSTRAINT PK_TonDauKy PRIMARY KEY(MaDonVi,MaHang),
 CONSTRAINT CK_TonDauKy_SoLuong CHECK(SoLuong>=0)
);
-- Shared commercial header with explicit type, partner and optional source order.
CREATE TABLE dbo.ChungTuThuongMai(
 ChungTuId bigint IDENTITY NOT NULL CONSTRAINT PK_ChungTuThuongMai PRIMARY KEY,
 MaDonVi varchar(20) NOT NULL CONSTRAINT FK_CTTM_DonVi REFERENCES dbo.DonVi(MaDonVi),
 Loai varchar(20) NOT NULL,
 SoChungTu varchar(30) NOT NULL, Ngay date NOT NULL,
 MaKH varchar(20) NULL CONSTRAINT FK_CTTM_KH REFERENCES dbo.KhachHang(MaKH),
 MaNCC varchar(20) NULL CONSTRAINT FK_CTTM_NCC REFERENCES dbo.NhaCungCap(MaNCC),
 TrangThai varchar(10) NOT NULL CONSTRAINT DF_CTTM_Status DEFAULT 'DRAFT',
 DonHangNguonId bigint NULL CONSTRAINT FK_CTTM_Order REFERENCES dbo.ChungTuThuongMai(ChungTuId),
 DienGiai nvarchar(500) NULL,
 PhienBan rowversion NOT NULL,
 CONSTRAINT UQ_CTTM_Number UNIQUE(MaDonVi,Loai,SoChungTu),
 CONSTRAINT CK_CTTM_Number CHECK(LEN(LTRIM(RTRIM(SoChungTu)))>0),
 CONSTRAINT CK_CTTM_TypePartner CHECK(
 (Loai IN ('SALES_ORDER','SALES_INVOICE') AND MaKH IS NOT NULL AND MaNCC IS NULL) OR
 (Loai IN ('PURCHASE_ORDER','PURCHASE_INVOICE') AND MaNCC IS NOT NULL AND MaKH IS NULL)),
 CONSTRAINT CK_CTTM_Status CHECK(TrangThai IN ('DRAFT','POSTED','CANCELLED')),
 CONSTRAINT CK_CTTM_Source CHECK(DonHangNguonId IS NULL OR DonHangNguonId<>ChungTuId)
);
CREATE TABLE dbo.ChungTuKho(
 ChungTuKhoId bigint IDENTITY NOT NULL CONSTRAINT PK_ChungTuKho PRIMARY KEY,
 MaDonVi varchar(20) NOT NULL CONSTRAINT FK_CTK_DonVi REFERENCES dbo.DonVi(MaDonVi),
 Loai char(1) NOT NULL, SoChungTu varchar(30) NOT NULL, Ngay date NOT NULL,
 TrangThai varchar(10) NOT NULL CONSTRAINT DF_CTK_Status DEFAULT 'DRAFT',
 ChungTuThuongMaiId bigint NULL CONSTRAINT FK_CTK_CTTM REFERENCES dbo.ChungTuThuongMai(ChungTuId),
 DienGiai nvarchar(500) NULL, PhienBan rowversion NOT NULL,
 CONSTRAINT UQ_CTK_Number UNIQUE(MaDonVi,Loai,SoChungTu),
 CONSTRAINT CK_CTK_Number CHECK(LEN(LTRIM(RTRIM(SoChungTu)))>0),
 CONSTRAINT CK_CTK_Loai CHECK(Loai IN ('N','X')),
 CONSTRAINT CK_CTK_Status CHECK(TrangThai IN ('DRAFT','POSTED','CANCELLED'))
);
CREATE TABLE dbo.CTChungTuThuongMai(
 ChungTuId bigint NOT NULL CONSTRAINT FK_CTChungTuThuongMai_Header REFERENCES dbo.ChungTuThuongMai(ChungTuId),
 Dong int NOT NULL,
 MaHang varchar(20) NOT NULL CONSTRAINT FK_CTChungTuThuongMai_Hang REFERENCES dbo.HangHoa(MaHang),
 SoLuong decimal(18,3) NOT NULL, DonGia decimal(19,4) NOT NULL,
 ThanhTien AS CONVERT(decimal(28,4),ROUND(SoLuong*DonGia,4)) PERSISTED,
 CONSTRAINT PK_CTChungTuThuongMai PRIMARY KEY(ChungTuId,Dong),
 CONSTRAINT CK_CTChungTuThuongMai_Values CHECK(Dong>0 AND SoLuong>0 AND DonGia>=0)
);
CREATE INDEX IX_CTChungTuThuongMai_Hang ON dbo.CTChungTuThuongMai(MaHang,ChungTuId) INCLUDE(SoLuong,DonGia);
CREATE TABLE dbo.CTChungTuKho(
 ChungTuKhoId bigint NOT NULL CONSTRAINT FK_CTChungTuKho_Header REFERENCES dbo.ChungTuKho(ChungTuKhoId),
 Dong int NOT NULL,
 MaHang varchar(20) NOT NULL CONSTRAINT FK_CTChungTuKho_Hang REFERENCES dbo.HangHoa(MaHang),
 SoLuong decimal(18,3) NOT NULL, DonGia decimal(19,4) NOT NULL,
 ThanhTien AS CONVERT(decimal(28,4),ROUND(SoLuong*DonGia,4)) PERSISTED,
 CONSTRAINT PK_CTChungTuKho PRIMARY KEY(ChungTuKhoId,Dong),
 CONSTRAINT CK_CTChungTuKho_Values CHECK(Dong>0 AND SoLuong>0 AND DonGia>=0)
);
CREATE INDEX IX_CTChungTuKho_Hang ON dbo.CTChungTuKho(MaHang,ChungTuKhoId) INCLUDE(SoLuong,DonGia);
CREATE TABLE dbo.TaiKhoan(
 MaTK varchar(20) NOT NULL CONSTRAINT PK_TaiKhoan PRIMARY KEY,
 TenTK nvarchar(200) NOT NULL,
 CONSTRAINT CK_TaiKhoan_Required CHECK(LEN(LTRIM(RTRIM(MaTK)))>0 AND LEN(LTRIM(RTRIM(TenTK)))>0)
);
CREATE TABLE dbo.PhieuKeToan(
 PhieuId bigint IDENTITY NOT NULL CONSTRAINT PK_PhieuKeToan PRIMARY KEY,
 MaDonVi varchar(20) NOT NULL CONSTRAINT FK_PKT_DonVi REFERENCES dbo.DonVi(MaDonVi),
 SoPhieu varchar(30) NOT NULL, Ngay date NOT NULL,
 TrangThai varchar(10) NOT NULL CONSTRAINT DF_PKT_Status DEFAULT 'DRAFT',
 DienGiai nvarchar(500) NULL, PhienBan rowversion NOT NULL,
 CONSTRAINT UQ_PKT_Number UNIQUE(MaDonVi,SoPhieu),
 CONSTRAINT CK_PKT_Number CHECK(LEN(LTRIM(RTRIM(SoPhieu)))>0),
 CONSTRAINT CK_PKT_Status CHECK(TrangThai IN ('DRAFT','POSTED','CANCELLED'))
);
CREATE TABLE dbo.CTPhieuKeToan(
 PhieuId bigint NOT NULL CONSTRAINT FK_CTPKT_Header REFERENCES dbo.PhieuKeToan(PhieuId),
 Dong int NOT NULL,
 MaTK varchar(20) NOT NULL CONSTRAINT FK_CTPKT_TK REFERENCES dbo.TaiKhoan(MaTK),
 PhatSinhNo decimal(28,4) NOT NULL CONSTRAINT DF_CTPKT_No DEFAULT 0,
 PhatSinhCo decimal(28,4) NOT NULL CONSTRAINT DF_CTPKT_Co DEFAULT 0,
 CONSTRAINT PK_CTPKT PRIMARY KEY(PhieuId,Dong),
 CONSTRAINT CK_CTPKT_Values CHECK(Dong>0 AND ((PhatSinhNo>0 AND PhatSinhCo=0) OR (PhatSinhCo>0 AND PhatSinhNo=0)))
);
CREATE INDEX IX_CTTM_Date ON dbo.ChungTuThuongMai(MaDonVi,Loai,TrangThai,Ngay) INCLUDE(MaKH,MaNCC);
CREATE INDEX IX_CTTM_KH ON dbo.ChungTuThuongMai(MaKH) WHERE MaKH IS NOT NULL;
CREATE INDEX IX_CTTM_NCC ON dbo.ChungTuThuongMai(MaNCC) WHERE MaNCC IS NOT NULL;
CREATE INDEX IX_CTTM_Order ON dbo.ChungTuThuongMai(DonHangNguonId) WHERE DonHangNguonId IS NOT NULL;
CREATE INDEX IX_CTK_Date ON dbo.ChungTuKho(MaDonVi,TrangThai,Ngay,Loai);
CREATE INDEX IX_CTK_Source ON dbo.ChungTuKho(ChungTuThuongMaiId) WHERE ChungTuThuongMaiId IS NOT NULL;
CREATE INDEX IX_PKT_Date ON dbo.PhieuKeToan(MaDonVi,TrangThai,Ngay);
CREATE INDEX IX_CTPKT_TK ON dbo.CTPhieuKeToan(MaTK);
CREATE INDEX IX_TonDauKy_Hang ON dbo.TonDauKy(MaHang);
CREATE TABLE dbo.SeedHistory(
 SeedName varchar(100) NOT NULL CONSTRAINT PK_SeedHistory PRIMARY KEY,
 AppliedAt datetime2(0) NOT NULL CONSTRAINT DF_SeedHistory_Date DEFAULT SYSUTCDATETIME()
);
INSERT dbo.SchemaVersion(Version) VALUES(1);
END;
COMMIT;
PRINT 'Schema version 1 ready.';



