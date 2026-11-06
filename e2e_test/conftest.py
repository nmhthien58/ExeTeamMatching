"""Hạ tầng chung: chạy một câu lệnh qua ArtemisClient, đọc JSON agent báo về, ghi kết quả từng lần chạy."""

import asyncio
import csv
import json
import urllib.request
import os
import re
import subprocess
import time
from pathlib import Path

import pytest
from artemis_client import ArtemisClient
from dotenv import load_dotenv

load_dotenv()

APP_PACKAGE = "vn.edu.fpt.exe.exe_team_matching"
EVIDENCE = Path(__file__).resolve().parent.parent / "evidence" / "artemis"
RESULTS = EVIDENCE / "results.csv"


def pytest_addoption(parser):
    parser.addoption("--runs", type=int, default=1, help="Số lần chạy mỗi ca (đề yêu cầu tối thiểu 3)")


def pytest_generate_tests(metafunc):
    if "run_index" in metafunc.fixturenames:
        metafunc.parametrize("run_index", range(1, metafunc.config.getoption("--runs") + 1))


def pytest_collection_modifyitems(items):
    enabled = {x.strip() for x in os.environ.get("ENABLED_PENDING", "").split(",") if x.strip()}
    for item in items:
        marker = item.get_closest_marker("pending")
        if marker and marker.args[0] not in enabled:
            item.add_marker(pytest.mark.skip(reason=f"Chờ {marker.args[0]}"))


def parse_output(output):
    """Agent trả dict hoặc chuỗi có JSON, kể cả JSON nằm trong khối markdown."""
    if isinstance(output, dict):
        return output
    match = re.search(r"\{.*\}", str(output or ""), re.DOTALL)
    if not match:
        raise AssertionError(f"Agent không trả JSON: {output!r}")
    return json.loads(match.group(0))


def final_report(base_url, session_id):
    """Phần giải thích của bước report_task_status cuối cùng – dùng khi Outputter không trả output
    (ARTEMIS 351ca84 qua /api/run không ghi output vào phiên)."""
    # Bước cuối có thể chưa được lưu ngay khi phiên báo xong: thử lại vài lần.
    for _ in range(6):
        with urllib.request.urlopen(f"{base_url.rstrip('/')}/api/sessions/{session_id}/steps", timeout=30) as resp:
            steps = json.loads(resp.read().decode("utf-8"))
        for step in reversed(steps if isinstance(steps, list) else steps.get("steps", [])):
            actions = step.get("action_taken") or []
            for action in actions if isinstance(actions, list) else [actions]:
                if action.get("action") == "report_task_status":
                    return (action.get("args") or {}).get("explanation")
        time.sleep(3)
    return None


def record(case_id, run_index, passed, seconds, trace_id, detail):
    EVIDENCE.mkdir(parents=True, exist_ok=True)
    is_new = not RESULTS.exists()
    with RESULTS.open("a", newline="", encoding="utf-8") as file:
        writer = csv.writer(file)
        if is_new:
            writer.writerow(["case", "run", "passed", "seconds", "trace_id", "detail"])
        writer.writerow([case_id, run_index, passed, f"{seconds:.1f}", trace_id, detail])


@pytest.hookimpl(hookwrapper=True)
def pytest_runtest_makereport(item, call):
    """Ghi results.csv theo kết quả cuối của ca (kể cả assert trên giá trị), không theo lúc agent trả JSON."""
    outcome = yield
    report = outcome.get_result()
    info = getattr(item, "_artemis", None)
    if report.when == "call" and info:
        detail = "" if report.passed else " ".join(str(call.excinfo.value).split())[:200]
        record(info["case"], info["run"], report.passed, info["seconds"], info["trace"], detail)


@pytest.fixture
def run_case(run_index, request):
    """Trả hàm chạy một ca: gửi câu lệnh, kiểm tra trạng thái, trả dict các trường agent báo về."""

    def run(case_id, goal, fields):
        expected = "Chỉ trả một object JSON với đúng các khóa: " + ", ".join(fields)
        # Câu lệnh tự nói rõ định dạng báo cáo cuối, để kết quả đọc được kể cả khi Outputter không chạy.
        goal = (goal + " Khi báo trạng thái hoàn thành, phần giải thích chỉ gồm một object JSON (không thêm chữ khác) "
                "với đúng các khóa: " + ", ".join(fields) + "; giá trị đúng/sai dùng true/false, số dùng số nguyên.")
        base_url = os.environ.get("ARTEMIS_BASE_URL", "http://127.0.0.1:8000")
        client = ArtemisClient(
            base_url,
            token=os.environ.get("ARTEMIS_TOKEN") or None,
            device_serial=os.environ.get("ARTEMIS_DEVICE_SERIAL") or None,
            default_profile=os.environ.get("ARTEMIS_PROFILE", "flash"),
        )
        started = time.monotonic()
        result = None
        try:
            result = asyncio.run(
                client.run(goal, expected_output=expected, enable_outputter=True, locked_app_package=APP_PACKAGE, timeout=600)
            )
            assert result.succeeded, f"{case_id}: {result.error or result.status}"
            values = parse_output(result.output if result.output is not None else final_report(base_url, result.trace_id))
            missing = [f for f in fields if f not in values]
            assert not missing, f"{case_id}: thiếu trường {missing}"
        finally:
            request.node._artemis = {
                "case": case_id, "run": run_index, "seconds": time.monotonic() - started,
                "trace": result.trace_id if result else "",
            }
        return values

    return run


def adb(*args):
    """Gọi ADB (biến ADB trỏ tới adb.exe nếu không có trong PATH); chọn thiết bị theo ARTEMIS_DEVICE_SERIAL."""
    serial = os.environ.get("ARTEMIS_DEVICE_SERIAL")
    cmd = [os.environ.get("ADB", "adb"), *(["-s", serial] if serial else []), *args]
    return subprocess.run(cmd, capture_output=True, text=True, encoding="utf-8", errors="replace", check=True).stdout


@pytest.fixture
def analytics_log():
    """Bật Analytics debug + log FA chi tiết, xóa Logcat trước ca; trả hàm đọc tên các sự kiện app đã ghi sau đó."""
    adb("shell", "setprop", "debug.firebase.analytics.app", APP_PACKAGE)
    adb("shell", "setprop", "log.tag.FA", "VERBOSE")
    adb("shell", "setprop", "log.tag.FA-SVC", "VERBOSE")
    adb("logcat", "-c")

    def events():
        dump = adb("logcat", "-d", "-s", "FA", "FA-SVC")
        names = re.findall(r"Logging event.*?name=([a-z_]+)", dump)
        (EVIDENCE / "logcat").mkdir(parents=True, exist_ok=True)
        (EVIDENCE / "logcat" / f"analytics-{int(time.time())}.txt").write_text(dump, encoding="utf-8")
        return names

    yield events
    adb("shell", "setprop", "debug.firebase.analytics.app", ".none.")
