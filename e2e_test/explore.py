"""Phiên khám phá (test-plan mục 6): một phiên không kịch bản, profile Pro, bắt đầu từ S01 với tài khoản sinh viên.

Chạy: `uv run python explore.py` trong `e2e_test/`. Kết quả ghi ở `evidence/artemis/explore-<giờ>.json`.
"""

import asyncio
import json
import os
import time
import urllib.request

from artemis_client import ArtemisClient
from dotenv import load_dotenv

from conftest import APP_PACKAGE, EVIDENCE, final_report

load_dotenv()

GOAL = (
    "Bạn là tester khám phá app EXE Team Matching (tiếng Việt), đang đăng nhập bằng tài khoản sinh viên. "
    "Bắt đầu từ Trang chủ. Phiên phải kéo dài ít nhất 15 phút và tối thiểu 60 thao tác; không báo hoàn thành sớm hơn. "
    "Đi qua các tab và màn hình (Trang chủ, Khám phá gồm cả tab Sinh viên tìm nhóm và bộ lọc, chi tiết nhóm, "
    "chi tiết sinh viên, Nhóm, Tạo nhóm (chỉ thử kiểm tra ô nhập, không bấm tạo khi đã điền đủ), Tin nhắn, Thông báo, "
    "avatar góc phải gồm Hồ sơ, Chỉnh sửa hồ sơ (không lưu), Cài đặt, và trong Cài đặt mục Demo Firebase → Thử AI). "
    "Ở mỗi màn cuộn hết trang, thử các thao tác thường gặp và thao tác dễ lỗi: tìm kiếm không có kết quả, "
    "ô nhập để trống, bấm Back hệ thống, xoay hoặc kéo làm mới. Không gửi lời mời, không xin vào nhóm, không đăng xuất, "
    "không xóa dữ liệu. Ghi lại mọi chỗ bất thường: lỗi hiện ra, nút không phản hồi, chữ bị cắt hoặc tràn, "
    "chữ tiếng Anh lẫn vào, màn trắng, tải mãi không xong. Khi xong, phần giải thích chỉ gồm một object JSON với khóa "
    "issues: mảng các object {screen, issue, severity (cao/trung bình/thấp)}, và khóa visited: mảng tên màn đã xem."
)


def last_thinking(base_url, session_id):
    """Phiên khám phá kết thúc bằng bước không có action; báo cáo JSON nằm ở phần suy luận của bước đó."""
    with urllib.request.urlopen(f"{base_url.rstrip('/')}/api/sessions/{session_id}/steps", timeout=30) as resp:
        steps = json.loads(resp.read().decode("utf-8"))
    return steps[-1].get("operator_raw_thinking") if steps else None


def main():
    base_url = os.environ.get("ARTEMIS_BASE_URL", "http://127.0.0.1:8000")
    client = ArtemisClient(
        base_url,
        token=os.environ.get("ARTEMIS_TOKEN") or None,
        device_serial=os.environ.get("ARTEMIS_DEVICE_SERIAL") or None,
        default_profile="pro",
    )
    started = time.monotonic()
    result = asyncio.run(client.run(GOAL, enable_outputter=True, locked_app_package=APP_PACKAGE, timeout=1500))
    report = result.output or final_report(base_url, result.trace_id) or last_thinking(base_url, result.trace_id)
    data = {
        "trace_id": result.trace_id,
        "status": str(result.status),
        "succeeded": result.succeeded,
        "minutes": round((time.monotonic() - started) / 60, 1),
        "report": report,
        "error": result.error,
    }
    EVIDENCE.mkdir(parents=True, exist_ok=True)
    out = EVIDENCE / f"explore-{int(time.time())}.json"
    out.write_text(json.dumps(data, ensure_ascii=False, indent=2, default=str), encoding="utf-8")
    print(out)


if __name__ == "__main__":
    import sys

    if len(sys.argv) > 1:  # Chỉ lấy lại kết quả của một phiên đã chạy: `explore.py <session_id>`.
        sid = sys.argv[1]
        base = os.environ.get("ARTEMIS_BASE_URL", "http://127.0.0.1:8000")
        out = EVIDENCE / f"explore-{sid[:8]}.json"
        out.write_text(json.dumps({"trace_id": sid, "report": last_thinking(base, sid)}, ensure_ascii=False, indent=2), encoding="utf-8")
        print(out)
    else:
        main()
