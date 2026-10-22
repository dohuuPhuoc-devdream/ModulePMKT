SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
GO
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaVersion WHERE Version=13)
BEGIN
BEGIN TRANSACTION;
DECLARE @Lock int;
EXEC @Lock=sys.sp_getapplock @Resource=N'ReportingModule.Schema',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;
IF @Lock<0 THROW 52300,'Cannot acquire migration lock.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaVersion WHERE Version=12) THROW 52301,'Schema version 12 required.',1;
ALTER TABLE dbo.TaiKhoan ADD SoTTCap1 int NULL;
ALTER TABLE dbo.TaiKhoan ADD LoaiTaiKhoan nvarchar(100) NULL;
CREATE TABLE #TT99Accounts(MaTK varchar(20) PRIMARY KEY,TenTK nvarchar(200),MaTKCha varchar(20) NULL,Cap tinyint,SoTTCap1 int NULL,LoaiTaiKhoan nvarchar(100),LaTaiKhoanLa bit);
END;
GO
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaVersion WHERE Version=13)
BEGIN
INSERT #TT99Accounts(MaTK,TenTK,MaTKCha,Cap,SoTTCap1,LoaiTaiKhoan,LaTaiKhoanLa) VALUES
 ('111',N'Tiền mặt',NULL,1,1,N'LOẠI TÀI KHOẢN TÀI SẢN',1),
 ('112',N'Tiền gửi không kỳ hạn',NULL,1,2,N'LOẠI TÀI KHOẢN TÀI SẢN',1),
 ('113',N'Tiền đang chuyển',NULL,1,3,N'LOẠI TÀI KHOẢN TÀI SẢN',1),
 ('121',N'Chứng khoán kinh doanh',NULL,1,4,N'LOẠI TÀI KHOẢN TÀI SẢN',1),
 ('128',N'Đầu tư nắm giữ đến ngày đáo hạn',NULL,1,5,N'LOẠI TÀI KHOẢN TÀI SẢN',0),
 ('1281',N'Tiền gửi có kỳ hạn','128',2,NULL,N'LOẠI TÀI KHOẢN TÀI SẢN',1),
 ('1282',N'Trái phiếu','128',2,NULL,N'LOẠI TÀI KHOẢN TÀI SẢN',1),
 ('1283',N'Cho vay','128',2,NULL,N'LOẠI TÀI KHOẢN TÀI SẢN',1),
 ('1288',N'Các khoản đầu tư khác nắm giữ đến ngày đáo hạn','128',2,NULL,N'LOẠI TÀI KHOẢN TÀI SẢN',1),
 ('131',N'Phải thu của khách hàng',NULL,1,6,N'LOẠI TÀI KHOẢN TÀI SẢN',1),
 ('133',N'Thuế GTGT được khấu trừ',NULL,1,7,N'LOẠI TÀI KHOẢN TÀI SẢN',0),
 ('1331',N'Thuế GTGT được khấu trừ của hàng hóa, dịch vụ','133',2,NULL,N'LOẠI TÀI KHOẢN TÀI SẢN',1),
 ('1332',N'Thuế GTGT được khấu trừ của TSCĐ','133',2,NULL,N'LOẠI TÀI KHOẢN TÀI SẢN',1),
 ('136',N'Phải thu nội bộ',NULL,1,8,N'LOẠI TÀI KHOẢN TÀI SẢN',0),
 ('1361',N'Vốn kinh doanh ở đơn vị trực thuộc','136',2,NULL,N'LOẠI TÀI KHOẢN TÀI SẢN',1),
 ('1362',N'Phải thu nội bộ về chênh lệch tỷ giá','136',2,NULL,N'LOẠI TÀI KHOẢN TÀI SẢN',1),
 ('1363',N'Phải thu nội bộ về chi phí đi vay đủ điều kiện được vốn hoá','136',2,NULL,N'LOẠI TÀI KHOẢN TÀI SẢN',1),
 ('1368',N'Phải thu nội bộ khác','136',2,NULL,N'LOẠI TÀI KHOẢN TÀI SẢN',1),
 ('138',N'Phải thu khác',NULL,1,9,N'LOẠI TÀI KHOẢN TÀI SẢN',0),
 ('1381',N'Tài sản thiếu chờ xử lý','138',2,NULL,N'LOẠI TÀI KHOẢN TÀI SẢN',1),
 ('1383',N'Thuế TTĐB của hàng nhập khẩu','138',2,NULL,N'LOẠI TÀI KHOẢN TÀI SẢN',1),
 ('1388',N'Phải thu khác','138',2,NULL,N'LOẠI TÀI KHOẢN TÀI SẢN',1),
 ('141',N'Tạm ứng',NULL,1,10,N'LOẠI TÀI KHOẢN TÀI SẢN',1),
 ('151',N'Hàng mua đang đi đường',NULL,1,11,N'LOẠI TÀI KHOẢN TÀI SẢN',1),
 ('152',N'Nguyên liệu, vật liệu',NULL,1,12,N'LOẠI TÀI KHOẢN TÀI SẢN',1),
 ('153',N'Công cụ, dụng cụ',NULL,1,13,N'LOẠI TÀI KHOẢN TÀI SẢN',1),
 ('154',N'Chi phí sản xuất, kinh doanh dở dang',NULL,1,14,N'LOẠI TÀI KHOẢN TÀI SẢN',1),
 ('155',N'Sản phẩm',NULL,1,15,N'LOẠI TÀI KHOẢN TÀI SẢN',1),
 ('156',N'Hàng hóa',NULL,1,16,N'LOẠI TÀI KHOẢN TÀI SẢN',1),
 ('157',N'Hàng gửi đi bán',NULL,1,17,N'LOẠI TÀI KHOẢN TÀI SẢN',1),
 ('158',N'Nguyên liệu, vật tư tại kho bảo thuế',NULL,1,18,N'LOẠI TÀI KHOẢN TÀI SẢN',1),
 ('171',N'Giao dịch mua, bán lại trái phiếu chính phủ',NULL,1,19,N'LOẠI TÀI KHOẢN TÀI SẢN',1),
 ('211',N'Tài sản cố định hữu hình',NULL,1,20,N'LOẠI TÀI KHOẢN TÀI SẢN',1),
 ('212',N'Tài sản cố định thuê tài chính',NULL,1,21,N'LOẠI TÀI KHOẢN TÀI SẢN',1),
 ('213',N'Tài sản cố định vô hình',NULL,1,22,N'LOẠI TÀI KHOẢN TÀI SẢN',1),
 ('214',N'Hao mòn tài sản cố định',NULL,1,23,N'LOẠI TÀI KHOẢN TÀI SẢN',0),
 ('2141',N'Hao mòn TSCĐ hữu hình','214',2,NULL,N'LOẠI TÀI KHOẢN TÀI SẢN',1),
 ('2142',N'Hao mòn TSCĐ thuê tài chính','214',2,NULL,N'LOẠI TÀI KHOẢN TÀI SẢN',1),
 ('2143',N'Hao mòn TSCĐ vô hình','214',2,NULL,N'LOẠI TÀI KHOẢN TÀI SẢN',1),
 ('2147',N'Hao mòn BĐSĐT','214',2,NULL,N'LOẠI TÀI KHOẢN TÀI SẢN',1),
 ('215',N'Tài sản sinh học',NULL,1,24,N'LOẠI TÀI KHOẢN TÀI SẢN',0),
 ('2151',N'Súc vật nuôi cho sản phẩm định kỳ','215',2,NULL,N'LOẠI TÀI KHOẢN TÀI SẢN',0),
 ('21511',N'Súc vật nuôi cho sản phẩm định kỳ chưa đạt đến giai đoạn trưởng thành','2151',3,NULL,N'LOẠI TÀI KHOẢN TÀI SẢN',1),
 ('21512',N'Súc vật nuôi cho sản phẩm định kỳ đạt đến giai đoạn trưởng thành','2151',3,NULL,N'LOẠI TÀI KHOẢN TÀI SẢN',0),
 ('215121',N'Nguyên giá','21512',4,NULL,N'LOẠI TÀI KHOẢN TÀI SẢN',1),
 ('215122',N'Giá trị khấu hao lũy kế','21512',4,NULL,N'LOẠI TÀI KHOẢN TÀI SẢN',1),
 ('2152',N'Súc vật nuôi lấy sản phẩm một lần','215',2,NULL,N'LOẠI TÀI KHOẢN TÀI SẢN',1),
 ('2153',N'Cây trồng theo mùa vụ hoặc lấy sản phẩm một lần','215',2,NULL,N'LOẠI TÀI KHOẢN TÀI SẢN',1),
 ('217',N'Bất động sản đầu tư',NULL,1,25,N'LOẠI TÀI KHOẢN TÀI SẢN',1),
 ('221',N'Đầu tư vào công ty con',NULL,1,26,N'LOẠI TÀI KHOẢN TÀI SẢN',1),
 ('222',N'Đầu tư vào công ty liên doanh, liên kết',NULL,1,27,N'LOẠI TÀI KHOẢN TÀI SẢN',1),
 ('228',N'Đầu tư khác',NULL,1,28,N'LOẠI TÀI KHOẢN TÀI SẢN',0),
 ('2281',N'Đầu tư góp vốn vào đơn vị khác','228',2,NULL,N'LOẠI TÀI KHOẢN TÀI SẢN',1),
 ('2288',N'Đầu tư khác','228',2,NULL,N'LOẠI TÀI KHOẢN TÀI SẢN',1),
 ('229',N'Dự phòng tổn thất tài sản',NULL,1,29,N'LOẠI TÀI KHOẢN TÀI SẢN',0),
 ('2291',N'Dự phòng giảm giá chứng khoán kinh doanh','229',2,NULL,N'LOẠI TÀI KHOẢN TÀI SẢN',1),
 ('2292',N'Dự phòng tổn thất đầu tư vào đơn vị khác','229',2,NULL,N'LOẠI TÀI KHOẢN TÀI SẢN',1),
 ('2293',N'Dự phòng phải thu khó đòi','229',2,NULL,N'LOẠI TÀI KHOẢN TÀI SẢN',1),
 ('2294',N'Dự phòng giảm giá hàng tồn kho','229',2,NULL,N'LOẠI TÀI KHOẢN TÀI SẢN',1),
 ('2295',N'Dự phòng tổn thất tài sản sinh học','229',2,NULL,N'LOẠI TÀI KHOẢN TÀI SẢN',1),
 ('241',N'Xây dựng cơ bản dở dang',NULL,1,30,N'LOẠI TÀI KHOẢN TÀI SẢN',0),
 ('2411',N'Mua sắm TSCĐ','241',2,NULL,N'LOẠI TÀI KHOẢN TÀI SẢN',1),
 ('2412',N'Xây dựng cơ bản','241',2,NULL,N'LOẠI TÀI KHOẢN TÀI SẢN',1),
 ('2413',N'Sửa chữa, bảo dưỡng định kỳ TSCĐ','241',2,NULL,N'LOẠI TÀI KHOẢN TÀI SẢN',1),
 ('2414',N'Nâng cấp, cải tạo TSCĐ','241',2,NULL,N'LOẠI TÀI KHOẢN TÀI SẢN',1),
 ('242',N'Chi phí chờ phân bổ',NULL,1,31,N'LOẠI TÀI KHOẢN TÀI SẢN',1),
 ('243',N'Tài sản thuế thu nhập hoãn lại',NULL,1,32,N'LOẠI TÀI KHOẢN TÀI SẢN',1),
 ('244',N'Ký quỹ, ký cược',NULL,1,33,N'LOẠI TÀI KHOẢN TÀI SẢN',1),
 ('331',N'Phải trả cho người bán',NULL,1,34,N'LOẠI TÀI KHOẢN NỢ PHẢI TRẢ',1),
 ('332',N'Phải trả cổ tức, lợi nhuận',NULL,1,35,N'LOẠI TÀI KHOẢN NỢ PHẢI TRẢ',1),
 ('333',N'Thuế và các khoản phải nộp Nhà nước',NULL,1,36,N'LOẠI TÀI KHOẢN NỢ PHẢI TRẢ',0),
 ('3331',N'Thuế giá trị gia tăng phải nộp','333',2,NULL,N'LOẠI TÀI KHOẢN NỢ PHẢI TRẢ',0),
 ('33311',N'Thuế GTGT đầu ra','3331',3,NULL,N'LOẠI TÀI KHOẢN NỢ PHẢI TRẢ',1),
 ('33312',N'Thuế GTGT hàng nhập khẩu','3331',3,NULL,N'LOẠI TÀI KHOẢN NỢ PHẢI TRẢ',1),
 ('3332',N'Thuế tiêu thụ đặc biệt','333',2,NULL,N'LOẠI TÀI KHOẢN NỢ PHẢI TRẢ',1),
 ('3333',N'Thuế xuất, nhập khẩu','333',2,NULL,N'LOẠI TÀI KHOẢN NỢ PHẢI TRẢ',1),
 ('3334',N'Thuế thu nhập doanh nghiệp','333',2,NULL,N'LOẠI TÀI KHOẢN NỢ PHẢI TRẢ',1),
 ('3335',N'Thuế thu nhập cá nhân','333',2,NULL,N'LOẠI TÀI KHOẢN NỢ PHẢI TRẢ',1),
 ('3336',N'Thuế tài nguyên','333',2,NULL,N'LOẠI TÀI KHOẢN NỢ PHẢI TRẢ',1),
 ('3337',N'Thuế nhà đất, tiền thuê đất','333',2,NULL,N'LOẠI TÀI KHOẢN NỢ PHẢI TRẢ',1),
 ('3338',N'Thuế bảo vệ môi trường và các loại thuế khác','333',2,NULL,N'LOẠI TÀI KHOẢN NỢ PHẢI TRẢ',0),
 ('33381',N'Thuế bảo vệ môi trường','3338',3,NULL,N'LOẠI TÀI KHOẢN NỢ PHẢI TRẢ',1),
 ('33382',N'Các loại thuế khác','3338',3,NULL,N'LOẠI TÀI KHOẢN NỢ PHẢI TRẢ',1),
 ('3339',N'Phí, lệ phí và các khoản phải nộp khác','333',2,NULL,N'LOẠI TÀI KHOẢN NỢ PHẢI TRẢ',1),
 ('334',N'Phải trả người lao động',NULL,1,37,N'LOẠI TÀI KHOẢN NỢ PHẢI TRẢ',1),
 ('335',N'Chi phí phải trả',NULL,1,38,N'LOẠI TÀI KHOẢN NỢ PHẢI TRẢ',1),
 ('336',N'Phải trả nội bộ',NULL,1,39,N'LOẠI TÀI KHOẢN NỢ PHẢI TRẢ',0),
 ('3361',N'Phải trả nội bộ về vốn kinh doanh','336',2,NULL,N'LOẠI TÀI KHOẢN NỢ PHẢI TRẢ',1),
 ('3362',N'Phải trả nội bộ về chênh lệch tỷ giá','336',2,NULL,N'LOẠI TÀI KHOẢN NỢ PHẢI TRẢ',1),
 ('3363',N'Phải trả nội bộ về chi phí đi vay đủ điều kiện được vốn hóa','336',2,NULL,N'LOẠI TÀI KHOẢN NỢ PHẢI TRẢ',1),
 ('3368',N'Phải trả nội bộ khác','336',2,NULL,N'LOẠI TÀI KHOẢN NỢ PHẢI TRẢ',1),
 ('337',N'Thanh toán theo tiến độ hợp đồng xây dựng',NULL,1,40,N'LOẠI TÀI KHOẢN NỢ PHẢI TRẢ',1),
 ('338',N'Phải trả, phải nộp khác',NULL,1,41,N'LOẠI TÀI KHOẢN NỢ PHẢI TRẢ',0),
 ('3381',N'Tài sản thừa chờ giải quyết','338',2,NULL,N'LOẠI TÀI KHOẢN NỢ PHẢI TRẢ',1),
 ('3382',N'Kinh phí công đoàn','338',2,NULL,N'LOẠI TÀI KHOẢN NỢ PHẢI TRẢ',1),
 ('3383',N'Bảo hiểm xã hội','338',2,NULL,N'LOẠI TÀI KHOẢN NỢ PHẢI TRẢ',1),
 ('3384',N'Bảo hiểm y tế','338',2,NULL,N'LOẠI TÀI KHOẢN NỢ PHẢI TRẢ',1),
 ('3386',N'Bảo hiểm thất nghiệp','338',2,NULL,N'LOẠI TÀI KHOẢN NỢ PHẢI TRẢ',1),
 ('3387',N'Doanh thu chờ phân bổ','338',2,NULL,N'LOẠI TÀI KHOẢN NỢ PHẢI TRẢ',1),
 ('3388',N'Phải trả, phải nộp khác','338',2,NULL,N'LOẠI TÀI KHOẢN NỢ PHẢI TRẢ',1),
 ('341',N'Vay và nợ thuê tài chính',NULL,1,42,N'LOẠI TÀI KHOẢN NỢ PHẢI TRẢ',0),
 ('3411',N'Các khoản đi vay','341',2,NULL,N'LOẠI TÀI KHOẢN NỢ PHẢI TRẢ',1),
 ('3412',N'Nợ thuê tài chính','341',2,NULL,N'LOẠI TÀI KHOẢN NỢ PHẢI TRẢ',1),
 ('343',N'Trái phiếu phát hành',NULL,1,43,N'LOẠI TÀI KHOẢN NỢ PHẢI TRẢ',0),
 ('3431',N'Trái phiếu thường','343',2,NULL,N'LOẠI TÀI KHOẢN NỢ PHẢI TRẢ',1),
 ('3432',N'Trái phiếu chuyển đổi','343',2,NULL,N'LOẠI TÀI KHOẢN NỢ PHẢI TRẢ',1),
 ('344',N'Nhận ký quỹ, ký cược',NULL,1,44,N'LOẠI TÀI KHOẢN NỢ PHẢI TRẢ',1),
 ('347',N'Thuế thu nhập hoãn lại phải trả',NULL,1,45,N'LOẠI TÀI KHOẢN NỢ PHẢI TRẢ',1),
 ('352',N'Dự phòng phải trả',NULL,1,46,N'LOẠI TÀI KHOẢN NỢ PHẢI TRẢ',0),
 ('3521',N'Dự phòng bảo hành sản phẩm, hàng hóa','352',2,NULL,N'LOẠI TÀI KHOẢN NỢ PHẢI TRẢ',1),
 ('3522',N'Dự phòng bảo hành công trình xây dựng','352',2,NULL,N'LOẠI TÀI KHOẢN NỢ PHẢI TRẢ',1),
 ('3523',N'Dự phòng tái cơ cấu doanh nghiệp','352',2,NULL,N'LOẠI TÀI KHOẢN NỢ PHẢI TRẢ',1),
 ('3525',N'Dự phòng phải trả khác','352',2,NULL,N'LOẠI TÀI KHOẢN NỢ PHẢI TRẢ',1),
 ('353',N'Quỹ khen thưởng, phúc lợi',NULL,1,47,N'LOẠI TÀI KHOẢN NỢ PHẢI TRẢ',0),
 ('3531',N'Quỹ khen thưởng','353',2,NULL,N'LOẠI TÀI KHOẢN NỢ PHẢI TRẢ',1),
 ('3532',N'Quỹ phúc lợi','353',2,NULL,N'LOẠI TÀI KHOẢN NỢ PHẢI TRẢ',1),
 ('3533',N'Quỹ phúc lợi đã hình thành TSCĐ','353',2,NULL,N'LOẠI TÀI KHOẢN NỢ PHẢI TRẢ',1),
 ('3534',N'Quỹ thưởng ban quản lý điều hành công ty','353',2,NULL,N'LOẠI TÀI KHOẢN NỢ PHẢI TRẢ',1),
 ('356',N'Quỹ phát triển khoa học và công nghệ',NULL,1,48,N'LOẠI TÀI KHOẢN NỢ PHẢI TRẢ',0),
 ('3561',N'Quỹ phát triển khoa học và công nghệ','356',2,NULL,N'LOẠI TÀI KHOẢN NỢ PHẢI TRẢ',1),
 ('3562',N'Quỹ phát triển khoa học và công nghệ đã hình thành tài sản','356',2,NULL,N'LOẠI TÀI KHOẢN NỢ PHẢI TRẢ',1),
 ('357',N'Quỹ bình ổn giá',NULL,1,49,N'LOẠI TÀI KHOẢN NỢ PHẢI TRẢ',1),
 ('411',N'Vốn đầu tư của chủ sở hữu',NULL,1,50,N'LOẠI TÀI KHOẢN VỐN CHỦ SỞ HỮU',0),
 ('4111',N'Vốn góp của chủ sở hữu','411',2,NULL,N'LOẠI TÀI KHOẢN VỐN CHỦ SỞ HỮU',0),
 ('41111',N'Cổ phiếu phổ thông có quyền biểu quyết','4111',3,NULL,N'LOẠI TÀI KHOẢN VỐN CHỦ SỞ HỮU',1),
 ('41112',N'Cổ phiếu ưu đãi','4111',3,NULL,N'LOẠI TÀI KHOẢN VỐN CHỦ SỞ HỮU',1),
 ('4112',N'Thặng dư vốn','411',2,NULL,N'LOẠI TÀI KHOẢN VỐN CHỦ SỞ HỮU',1),
 ('4113',N'Quyền chọn chuyển đổi trái phiếu','411',2,NULL,N'LOẠI TÀI KHOẢN VỐN CHỦ SỞ HỮU',1),
 ('4118',N'Vốn khác','411',2,NULL,N'LOẠI TÀI KHOẢN VỐN CHỦ SỞ HỮU',1),
 ('412',N'Chênh lệch đánh giá lại tài sản',NULL,1,51,N'LOẠI TÀI KHOẢN VỐN CHỦ SỞ HỮU',1),
 ('413',N'Chênh lệch tỷ giá hối đoái',NULL,1,52,N'LOẠI TÀI KHOẢN VỐN CHỦ SỞ HỮU',1),
 ('414',N'Quỹ đầu tư phát triển',NULL,1,53,N'LOẠI TÀI KHOẢN VỐN CHỦ SỞ HỮU',1),
 ('418',N'Các quỹ khác thuộc vốn chủ sở hữu',NULL,1,54,N'LOẠI TÀI KHOẢN VỐN CHỦ SỞ HỮU',1),
 ('419',N'Cổ phiếu mua lại của chính mình',NULL,1,55,N'LOẠI TÀI KHOẢN VỐN CHỦ SỞ HỮU',1),
 ('421',N'Lợi nhuận sau thuế chưa phân phối',NULL,1,56,N'LOẠI TÀI KHOẢN VỐN CHỦ SỞ HỮU',0),
 ('4211',N'Lợi nhuận sau thuế chưa phân phối lũy kế đến cuối năm trước','421',2,NULL,N'LOẠI TÀI KHOẢN VỐN CHỦ SỞ HỮU',1),
 ('4212',N'Lợi nhuận sau thuế chưa phân phối năm nay','421',2,NULL,N'LOẠI TÀI KHOẢN VỐN CHỦ SỞ HỮU',1),
 ('511',N'Doanh thu bán hàng và cung cấp dịch vụ',NULL,1,57,N'LOẠI TÀI KHOẢN DOANH THU',1),
 ('515',N'Doanh thu hoạt động tài chính',NULL,1,58,N'LOẠI TÀI KHOẢN DOANH THU',1),
 ('521',N'Các khoản giảm trừ doanh thu',NULL,1,59,N'LOẠI TÀI KHOẢN DOANH THU',1),
 ('621',N'Chi phí nguyên liệu, vật liệu trực tiếp',NULL,1,60,N'LOẠI TÀI KHOẢN CHI PHÍ SẢN XUẤT, KINH DOANH',1),
 ('622',N'Chi phí nhân công trực tiếp',NULL,1,61,N'LOẠI TÀI KHOẢN CHI PHÍ SẢN XUẤT, KINH DOANH',1),
 ('623',N'Chi phí sử dụng máy thi công',NULL,1,62,N'LOẠI TÀI KHOẢN CHI PHÍ SẢN XUẤT, KINH DOANH',0),
 ('6231',N'Chi phí nhân công','623',2,NULL,N'LOẠI TÀI KHOẢN CHI PHÍ SẢN XUẤT, KINH DOANH',1),
 ('6232',N'Chi phí vật liệu','623',2,NULL,N'LOẠI TÀI KHOẢN CHI PHÍ SẢN XUẤT, KINH DOANH',1),
 ('6233',N'Chi phí dụng cụ sản xuất','623',2,NULL,N'LOẠI TÀI KHOẢN CHI PHÍ SẢN XUẤT, KINH DOANH',1),
 ('6234',N'Chi phí khấu hao máy thi công','623',2,NULL,N'LOẠI TÀI KHOẢN CHI PHÍ SẢN XUẤT, KINH DOANH',1),
 ('6237',N'Chi phí dịch vụ mua ngoài','623',2,NULL,N'LOẠI TÀI KHOẢN CHI PHÍ SẢN XUẤT, KINH DOANH',1),
 ('6238',N'Chi phí bằng tiền khác','623',2,NULL,N'LOẠI TÀI KHOẢN CHI PHÍ SẢN XUẤT, KINH DOANH',1),
 ('627',N'Chi phí sản xuất chung',NULL,1,63,N'LOẠI TÀI KHOẢN CHI PHÍ SẢN XUẤT, KINH DOANH',0),
 ('6271',N'Chi phí nhân viên phân xưởng','627',2,NULL,N'LOẠI TÀI KHOẢN CHI PHÍ SẢN XUẤT, KINH DOANH',1),
 ('6272',N'Chi phí vật liệu','627',2,NULL,N'LOẠI TÀI KHOẢN CHI PHÍ SẢN XUẤT, KINH DOANH',1),
 ('6273',N'Chi phí dụng cụ sản xuất','627',2,NULL,N'LOẠI TÀI KHOẢN CHI PHÍ SẢN XUẤT, KINH DOANH',1),
 ('6274',N'Chi phí khấu hao TSCĐ','627',2,NULL,N'LOẠI TÀI KHOẢN CHI PHÍ SẢN XUẤT, KINH DOANH',1),
 ('6275',N'Thuế, phí, lệ phí','627',2,NULL,N'LOẠI TÀI KHOẢN CHI PHÍ SẢN XUẤT, KINH DOANH',1),
 ('6277',N'Chi phí dịch vụ mua ngoài','627',2,NULL,N'LOẠI TÀI KHOẢN CHI PHÍ SẢN XUẤT, KINH DOANH',1),
 ('6278',N'Chi phí bằng tiền khác','627',2,NULL,N'LOẠI TÀI KHOẢN CHI PHÍ SẢN XUẤT, KINH DOANH',1),
 ('632',N'Giá vốn hàng bán',NULL,1,64,N'LOẠI TÀI KHOẢN CHI PHÍ SẢN XUẤT, KINH DOANH',1),
 ('635',N'Chi phí tài chính',NULL,1,65,N'LOẠI TÀI KHOẢN CHI PHÍ SẢN XUẤT, KINH DOANH',1),
 ('641',N'Chi phí bán hàng',NULL,1,66,N'LOẠI TÀI KHOẢN CHI PHÍ SẢN XUẤT, KINH DOANH',0),
 ('6411',N'Chi phí nhân viên','641',2,NULL,N'LOẠI TÀI KHOẢN CHI PHÍ SẢN XUẤT, KINH DOANH',1),
 ('6412',N'Chi phí vật liệu, bao bì','641',2,NULL,N'LOẠI TÀI KHOẢN CHI PHÍ SẢN XUẤT, KINH DOANH',1),
 ('6413',N'Chi phí dụng cụ, đồ dùng','641',2,NULL,N'LOẠI TÀI KHOẢN CHI PHÍ SẢN XUẤT, KINH DOANH',1),
 ('6414',N'Chi phí khấu hao TSCĐ','641',2,NULL,N'LOẠI TÀI KHOẢN CHI PHÍ SẢN XUẤT, KINH DOANH',1),
 ('6415',N'Thuế, phí, lệ phí','641',2,NULL,N'LOẠI TÀI KHOẢN CHI PHÍ SẢN XUẤT, KINH DOANH',1),
 ('6417',N'Chi phí dịch vụ mua ngoài','641',2,NULL,N'LOẠI TÀI KHOẢN CHI PHÍ SẢN XUẤT, KINH DOANH',1),
 ('6418',N'Chi phí bằng tiền khác','641',2,NULL,N'LOẠI TÀI KHOẢN CHI PHÍ SẢN XUẤT, KINH DOANH',1),
 ('642',N'Chi phí quản lý doanh nghiệp',NULL,1,67,N'LOẠI TÀI KHOẢN CHI PHÍ SẢN XUẤT, KINH DOANH',0),
 ('6421',N'Chi phí nhân viên quản lý','642',2,NULL,N'LOẠI TÀI KHOẢN CHI PHÍ SẢN XUẤT, KINH DOANH',1),
 ('6422',N'Chi phí vật liệu quản lý','642',2,NULL,N'LOẠI TÀI KHOẢN CHI PHÍ SẢN XUẤT, KINH DOANH',1),
 ('6423',N'Chi phí đồ dùng văn phòng','642',2,NULL,N'LOẠI TÀI KHOẢN CHI PHÍ SẢN XUẤT, KINH DOANH',1),
 ('6424',N'Chi phí khấu hao TSCĐ','642',2,NULL,N'LOẠI TÀI KHOẢN CHI PHÍ SẢN XUẤT, KINH DOANH',1),
 ('6425',N'Thuế, phí và lệ phí','642',2,NULL,N'LOẠI TÀI KHOẢN CHI PHÍ SẢN XUẤT, KINH DOANH',1),
 ('6426',N'Chi phí dự phòng','642',2,NULL,N'LOẠI TÀI KHOẢN CHI PHÍ SẢN XUẤT, KINH DOANH',1),
 ('6427',N'Chi phí dịch vụ mua ngoài','642',2,NULL,N'LOẠI TÀI KHOẢN CHI PHÍ SẢN XUẤT, KINH DOANH',1),
 ('6428',N'Chi phí bằng tiền khác','642',2,NULL,N'LOẠI TÀI KHOẢN CHI PHÍ SẢN XUẤT, KINH DOANH',1),
 ('711',N'Thu nhập khác',NULL,1,68,N'LOẠI TÀI KHOẢN THU NHẬP KHÁC',1),
 ('811',N'Chi phí khác',NULL,1,69,N'LOẠI TÀI KHOẢN CHI PHÍ KHÁC',1),
 ('821',N'Chi phí thuế thu nhập doanh nghiệp',NULL,1,70,N'LOẠI TÀI KHOẢN CHI PHÍ KHÁC',0),
 ('8211',N'Chi phí thuế TNDN hiện hành','821',2,NULL,N'LOẠI TÀI KHOẢN CHI PHÍ KHÁC',0),
 ('82111',N'Chi phí thuế thu nhập doanh nghiệp hiện hành theo quy định của Luật thuế thu nhập doanh nghiệp','8211',3,NULL,N'LOẠI TÀI KHOẢN CHI PHÍ KHÁC',1),
 ('82112',N'Chi phí thuế thu nhập doanh nghiệp bổ sung theo quy định về thuế tối thiểu toàn cầu','8211',3,NULL,N'LOẠI TÀI KHOẢN CHI PHÍ KHÁC',1),
 ('8212',N'Chi phí thuế TNDN hoãn lại','821',2,NULL,N'LOẠI TÀI KHOẢN CHI PHÍ KHÁC',1),
 ('911',N'Xác định kết quả kinh doanh',NULL,1,71,N'TÀI KHOẢN XÁC ĐỊNH KẾT QUẢ KINH DOANH',1);
IF (SELECT COUNT(*) FROM #TT99Accounts)<>184 THROW 52302,'Unexpected TT99 account seed row count.',1;
IF EXISTS(SELECT 1 FROM dbo.TaiKhoan t JOIN #TT99Accounts a ON a.MaTK=t.MaTK WHERE t.TenTK<>a.TenTK OR ISNULL(t.MaTKCha,'')<>ISNULL(a.MaTKCha,'') OR t.Cap<>a.Cap OR (t.LoaiTaiKhoan IS NOT NULL AND t.LoaiTaiKhoan<>a.LoaiTaiKhoan))
 THROW 52303,'Existing account conflicts with the verified TT99 source; no account was overwritten.',1;
INSERT dbo.TaiKhoan(MaTK,TenTK,MaTKCha,Cap,TinhChat,ChoPhepHachToan,DoiTuong,HoatDong,SoTTCap1,LoaiTaiKhoan)
 SELECT a.MaTK,a.TenTK,a.MaTKCha,a.Cap,NULL,a.LaTaiKhoanLa,NULL,1,a.SoTTCap1,a.LoaiTaiKhoan
 FROM #TT99Accounts a WHERE NOT EXISTS(SELECT 1 FROM dbo.TaiKhoan t WHERE t.MaTK=a.MaTK);
UPDATE t SET SoTTCap1=a.SoTTCap1,LoaiTaiKhoan=a.LoaiTaiKhoan,ChoPhepHachToan=a.LaTaiKhoanLa
 FROM dbo.TaiKhoan t JOIN #TT99Accounts a ON a.MaTK=t.MaTK WHERE t.SoTTCap1 IS NULL OR t.LoaiTaiKhoan IS NULL;
END;
GO
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaVersion WHERE Version=13)
BEGIN

-- Retain historical journal facts while replacing the six internal teaching codes with their TT99 equivalents.
UPDATE d SET MaTK=m.NewCode FROM dbo.CTPhieuKeToan d JOIN (VALUES
 ('TS_TIEN','111'),('TS_KH','131'),('TS_HANG','156'),('NV_NCC','331'),('NV_VON','4111'),('NV_LN','4212')
) m(OldCode,NewCode) ON m.OldCode=d.MaTK;
DELETE oldMap FROM dbo.TaiKhoanBaoCao oldMap JOIN (VALUES
 ('TS_TIEN','111'),('TS_KH','131'),('TS_HANG','156'),('NV_NCC','331'),('NV_VON','4111'),('NV_LN','4212')
) m(OldCode,NewCode) ON oldMap.MaTK=m.OldCode WHERE EXISTS(SELECT 1 FROM dbo.TaiKhoanBaoCao newMap WHERE newMap.MaTK=m.NewCode);
UPDATE oldMap SET MaTK=m.NewCode FROM dbo.TaiKhoanBaoCao oldMap JOIN (VALUES
 ('TS_TIEN','111'),('TS_KH','131'),('TS_HANG','156'),('NV_NCC','331'),('NV_VON','4111'),('NV_LN','4212')
) m(OldCode,NewCode) ON oldMap.MaTK=m.OldCode WHERE NOT EXISTS(SELECT 1 FROM dbo.TaiKhoanBaoCao newMap WHERE newMap.MaTK=m.NewCode);
DELETE oldAccount FROM dbo.TaiKhoan oldAccount JOIN (VALUES
 ('TS_TIEN','111'),('TS_KH','131'),('TS_HANG','156'),('NV_NCC','331'),('NV_VON','4111'),('NV_LN','4212')
) m(OldCode,NewCode) ON oldAccount.MaTK=m.OldCode WHERE NOT EXISTS(SELECT 1 FROM dbo.CTPhieuKeToan d WHERE d.MaTK=oldAccount.MaTK);
IF EXISTS(SELECT 1 FROM dbo.TaiKhoan WHERE MaTK IN('TS_TIEN','TS_KH','TS_HANG','NV_NCC','NV_VON','NV_LN'))
 THROW 52304,'A teaching account remains referenced and could not be mapped.',1;
IF (SELECT COUNT(*) FROM dbo.TaiKhoan t JOIN #TT99Accounts a ON a.MaTK=t.MaTK WHERE t.TenTK=a.TenTK AND t.Cap=a.Cap AND t.ChoPhepHachToan=a.LaTaiKhoanLa)<>184
 THROW 52305,'TT99 account import verification failed.',1;
INSERT dbo.SchemaVersion(Version) VALUES(13);
COMMIT;
PRINT 'Verified TT99 Appendix II accounts loaded; teaching journal facts mapped; schema version 13.';
END;
GO
