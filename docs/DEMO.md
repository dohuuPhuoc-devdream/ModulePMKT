# Demo và nghiệm thu

Chạy setup/build theo README. Các bước trình duyệt yêu cầu IIS/IIS Express được cấu hình trước. Dùng dữ liệu giả lập và mã chứng từ mới; không sửa chứng từ đã ghi sổ.

## 1. Điều hướng và danh mục

Mở Thông tin chung. Kiểm tra đủ bảy menu ngang, mở menu lồng nhau bằng chuột/bàn phím; Escape đóng menu. Mở Danh mục khách hàng, tìm `KHDEMO001` (Công ty TNHH Minh Phát), rồi nhấn Làm tươi. Thử thêm/sửa/xóa một mã thử chưa có liên kết. Export lấy mẫu Excel; Import bản sao có mã mới, kiểm tra mã trùng bị từ chối, không ghi đè.

## 2. Tồn kho chuẩn

Sau `tests/run-phase4.ps1`, chọn DVDEMO, khoảng 01/01/2026–31/12/2026, HANG001 trong Nhập-Xuất-Tồn. Kỳ vọng đầu 100, nhập 50, xuất 20, cuối 130. PN001/PX001 đã được test tạo; không nhập lại hai chứng từ.

Tạo phiếu xuất mới, ngày sau PX001, HANG001 số lượng 200, giá không âm; thử ghi sổ. Kỳ vọng báo thiếu tồn, không có chứng từ đã ghi sổ hoặc chi tiết kho ghi dở; Làm tươi báo cáo vẫn 130. Nếu lưu nháp trước, bản nháp không làm giảm tồn.

## 3. Mua/bán và công nợ

Để không ảnh hưởng bộ nghiệm thu chuẩn, tạo một đơn vị mới với ngày bắt đầu 01/01/2026; dùng HANG002, `KHDEMO001` (Công ty TNHH Minh Phát) và `NCCDEMO001` (Công ty TNHH Vật tư Hòa Bình). Đây là dữ liệu giả lập.

1. Ghi sổ phiếu nhập mua ngày 02/01/2026 tại kho mặc định `KHO01`: `NCCDEMO001`, HANG002, số lượng 10, đơn giá 100 → tiền hàng 1.000, tồn 10.
2. Ghi sổ hóa đơn mua cùng nhà cung cấp, cùng đơn vị, cùng ngày hoặc sau ngày phiếu nhập; chi tiết mã hàng, số lượng và đơn giá phải khớp từng dòng phiếu nhập. Hóa đơn làm tăng công nợ, không nhập kho lần hai.
3. Ghi sổ hóa đơn bán ngày 03/01/2026: số lượng 3, giá 100 → doanh thu 300, tồn 7.
4. Trong Xem hóa đơn bán, ghi nhận thu 100 ngày 04/01/2026 → công nợ khách hàng cuối kỳ 200.
5. Trong Xem hóa đơn mua, ghi nhận trả 100 → công nợ nhà cung cấp cuối kỳ 900.
6. Xem các báo cáo chi tiết/phân tích, chọn đúng đơn vị và năm 2026. Phiếu nhập mua tạo biến động kho; hóa đơn mua chỉ tạo công nợ. Hóa đơn bán tạo biến động kho; các menu hóa đơn dùng chung dữ liệu.

Dùng đơn vị mới cho mỗi lần lặp demo để các số trên không cộng dồn. Các bước này là hướng dẫn, không được tự động tạo bởi Phase 9.

## 4. Tổng hợp

Trong cùng đơn vị thử, nhập phiếu thủ công: Nợ TS_TIEN 100, Có NV_VON 100; ghi sổ. Bảng cân đối cùng kỳ: tài sản 100, nguồn vốn 100, chênh lệch 0. Chứng từ mua/bán/thanh toán ở bước 3 không tự hạch toán vào sổ này. Thử phiếu lệch Nợ/Có để thấy kiểm tra ghi sổ.

## 5. Kết xuất và làm tươi

Áp dụng điều kiện báo cáo, đối chiếu grid và tổng. Mở In/Kết xuất, Excel và PDF; kiểm tra điều kiện, tất cả dòng và số tiền trùng grid. Thử sửa điều kiện chưa nhấn Nhận/Xem: kết xuất vẫn theo điều kiện đã áp dụng. Nhấn Làm tươi để truy vấn lại. Snapshot hết hạn cần quay lại báo cáo, lọc/làm tươi và tạo kết xuất mới.

Kiểm tra thủ công còn chờ môi trường: luồng trình duyệt trực tiếp với IIS, màn hình hẹp, hộp thoại in và bản in thực tế. Test tự động đã kiểm tra request/postback, SQL, file Excel/PDF; không thay thế các bước này.
