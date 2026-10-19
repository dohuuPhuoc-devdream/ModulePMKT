from pathlib import Path
from pypdf import PdfReader

path = Path("artifacts/phase11/purchase-receipt.pdf")
reader = PdfReader(path, strict=True)
assert len(reader.pages) == 1, f"expected one voucher page, found {len(reader.pages)}"
page = reader.pages[0]
assert float(page.mediabox.width) == 842
assert float(page.mediabox.height) == 595
text = page.extract_text() or ""
for value in (
    "Công ty thử", "Địa chỉ thử", "Mã số thuế: 0101234565", "PN-001",
    "Tổng cộng", "160.000", "Một trăm sáu mươi nghìn đồng chẵn", "Người giao hàng",
    "Người lập phiếu", "Người nhận hàng", "Thủ kho",
):
    assert value in text, f"searchable PDF text missing {value!r}: {text}"
assert len(text) > 150, f"PDF should contain selectable text, got {len(text)} characters"
print("PASS: QuestPDF voucher has searchable Vietnamese text, A4 landscape, totals, and four signature roles")
