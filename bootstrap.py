
import base64, sys
data = sys.stdin.read()
decoded = base64.b64decode(data).decode("utf-8")
with open(r"d:\Maik\Code\economic-empire-main\cross_ref_provinces.py", "w", encoding="utf-8") as f:
    f.write(decoded)
print("Written OK")
