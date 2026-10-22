# Phase 11 — Phiếu nhập mua PDF/Excel

Phiếu nhập mua xuất HTML, PDF và Excel từ cùng `VoucherModel`. Mẫu dùng tên pháp
lý/địa chỉ đơn vị nếu đã khai báo, dữ liệu nhà cung cấp và các dòng hàng đang lưu.
Vai trò ký gồm Người giao hàng, PT nhập hàng, Người nhận hàng và Thủ kho. Dòng
`Ngày……tháng……năm…………` có trong cả ba đầu ra. PDF dùng Times New Roman và chữ
vector có thể chọn/tìm.

## Kiểm tra tự động

```powershell
.\tests\run-phase11.ps1
```

Test xác nhận nhãn vai trò và ngày trong HTML/PDF/Excel, trích xuất văn bản tiếng
Việt từ PDF, kiểm tra phông serif, khổ giấy và tổng tiền. Để giữ PDF sinh bởi test
để xem bằng mắt:

```powershell
.\tests\run-phase11.ps1 -KeepArtifacts
```

PDF khi giữ lại nằm tại `artifacts/phase11/purchase-receipt.pdf`.

## Kiểm tra trực quan và in trên máy Windows

Sau khi tạo PDF, mở `artifacts/phase11/purchase-receipt.pdf` trong Edge hoặc Adobe
Acrobat:

1. Phóng to toàn trang và kiểm tra đầu trang, tiêu đề, bảng, dòng ngày, tổng tiền
   và bốn vùng chữ ký không bị cắt hoặc chồng lấn.
2. Dùng chuột chọn tên đơn vị, tên nhà cung cấp, tiếng Việt có dấu, `PT nhập hàng`
   và tổng tiền; thử Ctrl+F tìm `PT nhập hàng`.
3. Mở hộp thoại Print, chọn A4 ngang, xem Print Preview và kiểm tra chỉ một trang
   cho fixture một dòng.
4. Khi có máy in, in một bản ở chế độ nháp và xác nhận chữ có chân, dấu tiếng Việt,
   đường kẻ bảng và dòng ngày xuất hiện đúng trên giấy.

Trang PDF đã được render thành ảnh và xem bằng mắt; test cũng trích xuất được tiếng
Việt có dấu và xác nhận font Times New Roman trong PDF. Việc chọn/copy trực tiếp
trong trình đọc PDF, xem Print Preview và in ra giấy vẫn cần kiểm tra trên máy
Windows; kiểm tra tự động/render không được tính là in thử.
