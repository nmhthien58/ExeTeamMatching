"""Ca kiểm tra sự kiện Analytics đã được ghi (Lab 3 §7: ít nhất một ca đọc Logcat hoặc DebugView)."""


def test_tc21_view_team_event_logged(run_case, analytics_log):
    values = run_case(
        "TC-21",
        "Bắt đầu: tab Khám phá. Mở nhóm đầu tiên trong danh sách và đợi màn chi tiết tải xong. "
        "Cuối cùng: báo tên nhóm ở màn chi tiết.",
        ["team_name"],
    )
    assert values["team_name"]
    events = analytics_log()
    assert "view_team" in events, f"Logcat không có view_team, chỉ có: {sorted(set(events))}"
