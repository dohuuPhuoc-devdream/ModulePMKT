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
    "Công ty thử", "Địa chỉ thử", "Mã số thuế: 0101234565", "PN-001", "Nhà cung cấp",
    "Tổng cộng", "160.000", "Một trăm sáu mươi nghìn đồng chẵn", "Người giao hàng",
    "PT nhập hàng", "Người nhận hàng", "Thủ kho", "Ngày……tháng……năm…………",
):
    assert value in text, f"searchable PDF text missing {value!r}: {text}"
assert len(text) > 150, f"PDF should contain selectable text, got {len(text)} characters"
font_names = set()
for font in (page.get("/Resources") or {}).get("/Font", {}).values():
    font_names.add(str(font.get_object().get("/BaseFont", "")))
assert any("TimesNewRoman" in name or "TimesNewRomanPS" in name for name in font_names), f"expected embedded Times New Roman serif font, found {font_names}"
assert any("Bold" in name for name in font_names), f"expected a registered bold serif font for the voucher title, found {font_names}"
assert any("Italic" in name for name in font_names), f"expected a registered italic serif font for amount-in-words, found {font_names}"
fractional = PdfReader(Path("artifacts/phase11/fractional-purchase-receipt.pdf"), strict=True)
fractional_text = fractional.pages[0].extract_text() or ""
for value in ("101", "201", "302", "Ba trăm linh hai đồng chẵn"):
    assert value in fractional_text, f"fractional voucher output does not reconcile: missing {value!r}: {fractional_text}"
print("PASS: QuestPDF voucher has searchable Vietnamese text, A4 landscape, totals, and four signature roles")
