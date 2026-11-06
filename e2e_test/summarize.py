"""Đọc evidence/artemis/results.csv, in bảng kết quả Markdown cho docs/test-plan.md mục 5.

Chạy: uv run python summarize.py
"""

import csv
from collections import defaultdict
from pathlib import Path

RESULTS = Path(__file__).resolve().parent.parent / "evidence" / "artemis" / "results.csv"


def main():
    if not RESULTS.exists():
        print(f"Chưa có {RESULTS}. Chạy pytest trước.")
        return
    runs = defaultdict(list)
    with RESULTS.open(encoding="utf-8") as file:
        for row in csv.DictReader(file):
            runs[row["case"]].append(row)

    print("| Ca | Lần 1 | Lần 2 | Lần 3 | Tỷ lệ đạt | Thời gian trung bình | Trace (lần cuối) | Lỗi gần nhất |")
    print("|---|---|---|---|---|---|---|---|")
    for case in sorted(runs, key=lambda c: int(c.split("-")[1])):
        rows = runs[case][-3:]
        marks = ["✅" if r["passed"] == "True" else "❌" for r in rows] + ["–"] * (3 - len(rows))
        passed = sum(r["passed"] == "True" for r in rows)
        avg = sum(float(r["seconds"]) for r in rows) / len(rows)
        failed = [r["detail"] for r in rows if r["passed"] != "True"]
        print(f"| {case} | {' | '.join(marks)} | {passed}/{len(rows)} | {avg:.0f} s | `{rows[-1]['trace_id']}` | {failed[-1] if failed else ''} |")


if __name__ == "__main__":
    main()
