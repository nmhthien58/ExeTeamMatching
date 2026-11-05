"""Hạ tầng chung: chạy một câu lệnh qua ArtemisClient, đọc JSON agent báo về, ghi kết quả từng lần chạy."""

import asyncio
import csv
import json
import os
import re
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


def record(case_id, run_index, passed, seconds, trace_id, detail):
    EVIDENCE.mkdir(parents=True, exist_ok=True)
    is_new = not RESULTS.exists()
    with RESULTS.open("a", newline="", encoding="utf-8") as file:
        writer = csv.writer(file)
        if is_new:
            writer.writerow(["case", "run", "passed", "seconds", "trace_id", "detail"])
        writer.writerow([case_id, run_index, passed, f"{seconds:.1f}", trace_id, detail])


@pytest.fixture
def run_case(run_index):
    """Trả hàm chạy một ca: gửi câu lệnh, kiểm tra trạng thái, trả dict các trường agent báo về."""

    def run(case_id, goal, fields):
        expected = "Chỉ trả một object JSON với đúng các khóa: " + ", ".join(fields)
        client = ArtemisClient(
            os.environ.get("ARTEMIS_BASE_URL", "http://127.0.0.1:8000"),
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
            values = parse_output(result.output)
            missing = [f for f in fields if f not in values]
            assert not missing, f"{case_id}: thiếu trường {missing}"
        except Exception as error:
            trace = result.trace_id if result else ""
            record(case_id, run_index, False, time.monotonic() - started, trace, str(error)[:200])
            raise
        record(case_id, run_index, True, time.monotonic() - started, result.trace_id, "")
        return values

    return run
