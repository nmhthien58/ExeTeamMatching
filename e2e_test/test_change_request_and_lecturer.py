"""Luồng F2 (trưởng nhóm EXE201), F3 (giảng viên), bước mobile của luồng xuyên vai trò. Đổi tài khoản trước khi chạy từng nhóm ca."""

import pytest


@pytest.mark.pending("đợt đổi nhóm đang mở")
def test_tc06_submit_change_request(run_case):
    values = run_case(
        "TC-06",
        "Bắt đầu: tab Nhóm của trưởng nhóm EXE201. Mở Yêu cầu đổi nhóm, tạo yêu cầu mới, bỏ một thành viên, "
        "nhập lý do 'Thành viên chuyển sang nhóm khác', gửi. "
        "Cuối cùng: báo trạng thái yêu cầu ở màn chi tiết.",
        ["status"],
    )
    assert values["status"] == "Chờ duyệt"


@pytest.mark.pending("đợt đổi nhóm đang mở")
def test_tc07_short_reason_is_blocked(run_case):
    values = run_case(
        "TC-07",
        "Bắt đầu: màn tạo yêu cầu đổi nhóm. Nhập lý do 'abc' rồi bấm Gửi. "
        "Cuối cùng: báo dòng lỗi tại ô lý do và nội dung đang có trong ô.",
        ["error_text", "reason_value"],
    )
    assert values["error_text"]
    assert values["reason_value"] == "abc"


@pytest.mark.pending("tài khoản giảng viên")
def test_tc08_lecturer_opens_team(run_case):
    values = run_case(
        "TC-08",
        "Bắt đầu: tab Trang chủ của giảng viên. Mở tab Nhóm, mở nhóm đầu tiên. "
        "Cuối cùng: báo tên nhóm và số Checkpoint đã đánh dấu dạng a/4 nếu là nhóm EXE101.",
        ["team_name", "checkpoints"],
    )
    assert values["team_name"]


@pytest.mark.pending("tài khoản giảng viên")
def test_tc09_future_checkpoint_is_blocked(run_case):
    values = run_case(
        "TC-09",
        "Bắt đầu: màn chi tiết nhóm EXE101 của giảng viên. Đánh dấu một Checkpoint và thử chọn ngày mai. "
        "Cuối cùng: báo có chọn được ngày mai hay không và thông báo đang hiện, nếu có.",
        ["future_date_accepted", "message"],
    )
    assert values["future_date_accepted"] is False


@pytest.mark.pending("MB-52")
def test_tc18_cross_role_notification(run_case):
    values = run_case(
        "TC-18",
        "Bắt đầu: Bộ môn vừa duyệt yêu cầu đổi nhóm trên web. Mở tab Thông báo, chạm thông báo mới nhất về yêu cầu đổi nhóm. "
        "Cuối cùng: báo nội dung thông báo và trạng thái yêu cầu ở màn mở ra.",
        ["notification_text", "status"],
    )
    assert values["status"] == "Đã duyệt"
