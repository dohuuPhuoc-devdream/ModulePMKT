import csv
from collections import Counter
from pathlib import Path

path = Path("database/seed/tt99-accounts.csv")
with path.open(encoding="utf-8-sig", newline="") as source:
    rows = list(csv.DictReader(source))
assert len(rows) == 184, len(rows)
assert set(rows[0]) == {"MaTK", "TenTK", "Cap", "MaTKCha", "SoTTCap1", "LoaiTaiKhoan", "LaTaiKhoanLa"}
codes = [row["MaTK"] for row in rows]
assert len(set(codes)) == len(codes), "duplicate account code"
by_code = {row["MaTK"]: row for row in rows}
assert all(row["MaTKCha"] in by_code for row in rows if row["Cap"] != "1")
level1 = [row for row in rows if row["Cap"] == "1"]
assert len(level1) == 71
assert sorted(int(row["SoTTCap1"]) for row in level1) == list(range(1, 72))
child_counts = Counter(row["MaTKCha"] for row in rows if row["MaTKCha"])
for row in rows:
    assert int(row["LaTaiKhoanLa"]) == int(child_counts[row["MaTK"]] == 0), row["MaTK"]
expected = {
    "111": "Tiền mặt", "112": "Tiền gửi không kỳ hạn",
    "131": "Phải thu của khách hàng",
    "1331": "Thuế GTGT được khấu trừ của hàng hóa, dịch vụ",
    "156": "Hàng hóa", "331": "Phải trả cho người bán",
    "33311": "Thuế GTGT đầu ra",
    "511": "Doanh thu bán hàng và cung cấp dịch vụ",
    "632": "Giá vốn hàng bán",
    "4211": "Lợi nhuận sau thuế chưa phân phối lũy kế đến cuối năm trước",
    "4212": "Lợi nhuận sau thuế chưa phân phối năm nay",
    "911": "Xác định kết quả kinh doanh",
}
for code, name in expected.items():
    assert by_code[code]["TenTK"] == name, (code, by_code[code]["TenTK"])
print("PASS: TT99 source CSV has 184 unique, hierarchically consistent rows and 71 level-1 accounts")
