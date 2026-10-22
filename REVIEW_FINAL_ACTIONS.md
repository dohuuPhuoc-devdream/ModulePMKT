# ĐÁNH GIÁ & KẾ HOẠCH HOÀN THIỆN MODULE PMKT — FINAL UEH/FAST

**Repository:** https://github.com/dohuuPhuoc-devdream/ModulePMKT  
**Phạm vi kiểm tra:** mã nguồn nhánh `main` ngày 22/10/2026, tài liệu README, SQL migrations, các lớp xử lý kho/thương mại/thanh toán/bảo mật và tài liệu phase. **Chưa chạy ứng dụng/SQL trên máy Windows hoặc nghiệm thu giao diện với người dùng thật.**  
**Nguyên tắc:** chỉ ghi việc cần làm hoặc cần xác minh; không yêu cầu xây dựng ERP toàn diện. Các mục `[ ]` chỉ được đánh dấu hoàn tất sau khi có bằng chứng chạy thử.

## Kết luận phạm vi

Module có nền tảng đáng kể cho bài Final: ứng dụng ASP.NET Framework 4.8 Web Forms, SQL Server, danh mục, các luồng kho–mua/bán–công nợ, nhập bút toán thủ công, báo cáo, phân quyền và xuất PDF/Excel. Tuy nhiên **chưa nên gọi là hệ thống kế toán doanh nghiệp hoàn chỉnh hay đã tuân thủ đầy đủ TT99**. Tài liệu dự án xác nhận còn thiếu đối chiếu hóa đơn/phiếu nhập từng dòng, hạch toán tự động, nghiệp vụ thuế, sửa sai chứng từ đã ghi sổ và nghiệm thu E2E có đăng nhập. Định vị phù hợp: **module mô phỏng có phạm vi giới hạn, vận dụng một số quy trình kế toán doanh nghiệp**.

## P0 — Phải làm trước khi chốt Final / demo

- [ ] **P0.1 — Chứng minh nghiệm thu thật trên SQL Server + IIS Express.** Chạy `database/setup.sql`, build Release, `tests/run-all.ps1`, `tests/run-phase10.ps1` và kiểm tra lại từng kết quả. Đăng nhập bằng tài khoản thật, thực hiện CRUD + lưu nháp + ghi sổ + báo cáo + PDF/Excel trên trình duyệt. Lưu log, screenshot, phiên bản commit, ngày chạy, PASS/FAIL. *Nguồn:* `README.md`, `docs/PHASE10.md`, `docs/PHASE11.md`. **Tiêu chí:** tất cả kịch bản cốt lõi chạy qua không phụ thuộc lời mô tả.
- [ ] **P0.2 — Kiểm thử ma trận chứng từ/kho và liên kết mua–bán.** Tạo phiếu nhập 50 sau tồn 100, xuất 20 còn 130, từ chối xuất 200; thử xuất lùi ngày gây tồn âm tại thời điểm lịch sử, hai trình duyệt ghi sổ cùng lúc, cùng mặt hàng ở nhiều dòng, hai đơn vị khác nhau và nhiều kho. Phát hiện trường hợp tồn đủ tổng nhưng sai **kho cụ thể**; hiện procedure ghi tồn theo **đơn vị + mã hàng**, chưa rõ có bảo vệ âm riêng từng kho. *Nguồn:* `database/003-inventory.sql`, `database/008-purchase-workflow.sql`, `README.md`. **Tiêu chí:** tính tồn theo kho nhất quán với kho ghi trên chứng từ; nếu chỉ một kho/đơn vị, khóa chọn kho bổ sung và công bố rõ giới hạn.
- [ ] **P0.3 — Ràng buộc phân quyền toàn bộ đường ghi dữ liệu (server-side).** Xác minh từng thao tác SaveDraft/Post/Delete/Payment/Import/Export, từng màn hình và `MaDonVi` không thể vượt quyền bằng POST trực tiếp, đổi querystring hoặc sửa request. Kiểm tra **kiểm soát đơn vị trong SQL/stored procedure và lớp gọi**. *Nguồn:* `Security.cs`, `StockStore.cs`, `CommerceStore.cs`, `Web.config`. **Tiêu chí:** tài khoản không có quyền không thể tạo/sửa/xem/kết xuất dữ liệu ngoài đơn vị được gán.
- [ ] **P0.4 — Đối soát các báo cáo với chứng từ nguồn và trạng thái.** Lập một bảng kỳ vọng cho tồn đầu + nhập − xuất = tồn cuối; công nợ đầu + hóa đơn − thanh toán = cuối; doanh thu chỉ lấy hóa đơn đã ghi sổ; đơn hàng/nháp không phát sinh công nợ hoặc doanh thu. Kiểm tra lọc ngày, đơn vị, in/Excel/PDF có cùng tổng. *Nguồn:* `README.md`, `docs/PHASE5.md`–`docs/PHASE8.md`. **Tiêu chí:** số trong báo cáo truy ngược được đến chứng từ, không đếm trùng hóa đơn và phiếu kho.
- [ ] **P0.5 — Chốt tuyên bố pháp lý/chế độ kế toán.** Làm rõ TT99 trong dự án hiện mới có **danh mục tài khoản và nền tảng sổ**, không đồng nghĩa đã đáp ứng trọn yêu cầu sổ kế toán, nguyên tắc ghi nhận và báo cáo tài chính. Soát lại hiệu lực/đối tượng áp dụng văn bản trước khi nêu trong báo cáo Final, không tự khẳng định chứng nhận tuân thủ. *Nguồn:* `database/013-tt99-ledger-foundation.sql`, `database/014-tt99-accounts.sql`, `database/README.md`. **Tiêu chí:** có mục “phạm vi tuân thủ và giới hạn” trung thực, đối chiếu nguồn chính thức.

## P1 — Cải thiện tính đúng nghiệp vụ doanh nghiệp trong phạm vi đã chọn

- [ ] **P1.1 — Đối chiếu từng dòng hóa đơn mua và phiếu nhập mua**: mã hàng, số lượng, đơn giá, tổng tiền, đơn vị, nhà cung cấp, trạng thái; tránh hóa đơn không khớp hàng thực nhập. Định nghĩa xử lý chênh lệch rõ ràng (từ chối hoặc mô hình điều chỉnh). Hiện README thừa nhận liên kết một hóa đơn ↔ một phiếu nhập nhưng chưa đối chiếu từng dòng. *Nguồn:* `README.md`, `database/008-purchase-workflow.sql`.
- [ ] **P1.2 — Quy định giá trị tồn kho và giá vốn.** Tồn số lượng và đơn giá chứng từ **không tự đủ** để thành hệ thống kế toán kho: chọn rõ phương pháp tính giá xuất (ví dụ bình quân), số dư tồn đầu kỳ về **giá trị**, đối chiếu xuất kho với giá vốn và cách xử lý nhập trước/xuất sau. Nếu vượt thời gian làm Final, giới hạn báo cáo ở **tồn số lượng**, không gọi là giá trị tồn kho chuẩn. *Nguồn:* `database/001-schema.sql`, `database/003-inventory.sql`, `README.md`.
- [ ] **P1.3 — Định nghĩa quy trình hủy/điều chỉnh chứng từ đã ghi sổ.** Hiện chứng từ POSTED và thanh toán là bất biến, không có đảo/hủy. Với bản demo có thể dùng phiếu điều chỉnh hoặc quy trình lập chứng từ mới có tham chiếu chứng từ gốc, giữ audit, kiểm tra tác động đến tồn/công nợ. Không xóa trực tiếp dòng đã ghi sổ. *Nguồn:* `docs/PHASE10.md`, `README.md`.
- [ ] **P1.4 — Tách nội dung hạch toán khỏi diễn giải giao dịch.** Sổ tổng hợp nhập tay, chưa tự động định khoản từ mua/bán/kho/thanh toán. Nếu muốn gọi là “tích hợp kế toán”, thiết kế mapping tối thiểu + chứng từ nguồn + kiểm tra cân Nợ/Có + chống ghi trùng và đảo bút toán. Nếu không triển khai, mô tả là **sổ thực hành nhập độc lập**. *Nguồn:* `README.md`, `database/013-tt99-ledger-foundation.sql`.
- [ ] **P1.5 — Hoàn thiện các tình huống công nợ được đưa vào demo.** Kiểm tra trả nhiều lần, thanh toán vượt số dư, hai thanh toán đồng thời, trùng số tham chiếu, thanh toán sai đơn vị/kỳ; nêu rõ chưa có số dư đầu kỳ, ứng trước, hoàn/đảo thu chi. *Nguồn:* `src/ReportingModule.Web/Commerce.Payments.cs`, `README.md`.
- [ ] **P1.6 — Tính nhất quán chứng từ bán hàng**: trước khi POST hóa đơn, kiểm tra khách hàng, trạng thái và dòng hàng; phiếu xuất liên kết phải đúng đơn vị và không trừ kho hai lần; lặp yêu cầu không được tạo hai phiếu xuất. Xác minh xung đột ngày/kho khi xuất theo hóa đơn. *Nguồn:* `README.md`, `database/004-commerce.sql`, `database/008-purchase-workflow.sql`.
- [ ] **P1.7 — Kỳ kế toán và khóa sổ mức tối thiểu.** Chặn ghi/sửa chứng từ ngày đã chốt kỳ, ghi log thao tác khóa/mở có quyền riêng. Chỉ ưu tiên triển khai nếu Final thực sự giới thiệu “sổ kế toán” thay vì mô phỏng báo cáo. *Nguồn:* hiện trạng mô tả tại `README.md`.

## P2 — Chất lượng vận hành và trình bày Final (chỉ chọn khi đủ thời gian)

- [ ] **P2.1 — Làm rõ an toàn khi đưa ra Internet.** `Web.config` hiện `RequireHttps=false`, `requireSSL=false`, local SQL tin cậy chứng chỉ. Chỉ deploy công khai sau khi cấu hình HTTPS bắt buộc, cookie Secure, secret/key quản lý an toàn, SQL không mở cổng Internet, quyền DB tối thiểu, custom errors/logging, backup/restore và thử phục hồi. *Nguồn:* `src/ReportingModule.Web/Web.config`, `README.md`. **Không coi localhost demo tương đương production.**
- [ ] **P2.2 — Bổ sung nghiệm thu xuất biểu mẫu trên máy thật:** in A4, cắt trang, chữ tiếng Việt, font, ô chữ ký, PDF chọn/tìm text, mở Excel; so tổng tiền với DB và kiểm tra mốc 5.000 dòng. *Nguồn:* `docs/PHASE11.md`, `README.md`.
- [ ] **P2.3 — Chuẩn hóa dữ liệu minh họa:** tài khoản công ty giả, khách hàng, NCC, mã hàng, kho, đơn vị tính, số tiền, ngày phát sinh đủ để đi một vòng nghiệp vụ. Không đưa dữ liệu khách hàng FAST hoặc screenshot có thông tin nhạy cảm lên repo public. *Nguồn:* `database/002-seed.sql`, `database/012-demo-display-names.sql`.
- [ ] **P2.4 — Đóng gói chứng cứ cho Chương 3 Final:** sơ đồ kiến trúc, ERD, quy trình (nhập mua → kho → hóa đơn → thanh toán → báo cáo), use case, 6–10 ảnh màn hình, 5–8 bảng test case kèm Actual/Expected, mô tả rõ phần tự phát triển và điểm khác với FAST. Không nhận định đây là bản sao mã nguồn/hệ thống chính thức FAST. *Nguồn:* `README.md`, `docs/DEMO.md`, `database/README.md`.
- [ ] **P2.5 — Kiểm tra khả năng clone sạch:** trên môi trường mới, restore packages, chạy migrations theo thứ tự, provision admin an toàn, build + precompile + run test; bảo đảm không phụ thuộc DLL hoặc DB riêng máy phát triển. *Nguồn:* `README.md`, `tools/restore-packages.ps1`, `database/setup.sql`.

## Thứ tự thực hiện tối ưu cho kỳ Final

1. **P0.1 → P0.4 → P0.2 → P0.3 → P0.5**: chứng minh chạy thật, đúng số liệu, không sai tồn/quyền; chốt cách tuyên bố phạm vi.
2. **P1.1 + P1.2 + P1.5 + P1.6**: ưu tiên lỗi có thể làm sai số và sai nghiệp vụ của các phân hệ đã chọn.
3. **P1.3/P1.4/P1.7**: chọn “triển khai tối thiểu” **hoặc** “nêu giới hạn một cách chính xác”; không cần làm tất cả để được nộp bài.
4. **P2.2/P2.4/P2.5**: nghiệm thu, chuẩn bị minh chứng và chạy lại trên máy khác. **P2.1 chỉ bắt buộc khi deploy công khai.**

## Kịch bản demo được đề xuất

Tạo phiếu nhập 50 cho HANG001 (tồn đầu 100), POST; ghi phiếu xuất 20 và POST; kiểm tra tồn cuối 130; cố xuất 200 phải bị từ chối và DB không sinh bút toán thiếu; tạo hóa đơn mua gắn phiếu nhập và so chi tiết; ghi nhận thanh toán một phần; mở báo cáo kho, mua hàng, công nợ, tổng hợp; xuất cùng bộ dữ liệu sang PDF và Excel. Kiểm tra một tài khoản chỉ có quyền VIEW và một tài khoản khác đơn vị bị chặn ngay phía server.

## Tiêu chí chốt đề tài

**Đủ tin cậy để bảo vệ Final** khi các P0 có chứng cứ PASS, P1 liên quan số liệu được xử lý hoặc thuyết minh giới hạn rõ ràng, báo cáo diễn đạt “module mô phỏng nghiệp vụ thuộc một số phân hệ kế toán” chứ không gọi là ERP hoàn chỉnh. **Không có cơ sở tuyên bố sẵn sàng triển khai doanh nghiệp thực tế** khi chưa chạy kiểm thử vận hành, pháp lý, kế toán và bảo mật toàn diện.

---
*Phương pháp đánh giá: static review trên các đường dẫn nêu trên. Các rủi ro kiểm chứng là yêu cầu xác minh, không phải kết luận đã phát hiện lỗi runtime.*
