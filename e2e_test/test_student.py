"""Đăng nhập, luồng F1, tab, màn chi tiết, Remote Config, đăng xuất. Tài khoản: sinh viên chưa có nhóm (docs/test-plan.md)."""

import os

import pytest


def test_tc01_session_restored(run_case):
    values = run_case(
        "TC-01",
        "Bắt đầu: buộc dừng app EXE Team Matching rồi mở lại. Không bấm gì, đợi hết màn khởi động. "
        "Cuối cùng: báo tab đang chọn ở thanh dưới, và màn đăng nhập có hiện hay không.",
        ["selected_tab", "login_screen_shown"],
    )
    assert values["login_screen_shown"] is False
    assert values["selected_tab"] == "Trang chủ"


@pytest.mark.pending("tài khoản Google ngoài danh sách")
def test_tc02_email_not_in_list(run_case):
    values = run_case(
        "TC-02",
        "Bắt đầu: màn đăng nhập. Bấm đăng nhập Google, chọn tài khoản không có trong danh sách của học kỳ. "
        "Cuối cùng: báo tiêu đề màn đang hiện.",
        ["screen_title"],
    )
    assert "chưa có trong danh sách" in values["screen_title"].lower()


def test_tc03_search_team(run_case):
    values = run_case(
        "TC-03",
        "Bắt đầu: tab Trang chủ. Mở tab Khám phá, đọc tên nhóm đầu tiên, gõ đúng tên đó vào ô tìm kiếm rồi đợi kết quả. "
        "Cuối cùng: báo tên đã tìm, tên nhóm đầu tiên trong kết quả và số thẻ nhóm đang thấy.",
        ["searched_name", "first_result_name", "visible_count"],
    )
    assert values["first_result_name"] == values["searched_name"]
    assert values["visible_count"] >= 1


def test_tc04_team_detail(run_case):
    values = run_case(
        "TC-04",
        "Bắt đầu: tab Khám phá. Mở nhóm đầu tiên trong danh sách. "
        "Cuối cùng: báo tên nhóm ở màn chi tiết, số thành viên hiện tại và tối đa dạng a/b.",
        ["team_name", "members"],
    )
    assert values["team_name"]
    assert "/" in str(values["members"])


def test_tc05_join_without_idea_is_blocked(run_case):
    values = run_case(
        "TC-05",
        "Bắt đầu: màn chi tiết một nhóm ở tab Khám phá. Bấm Xin tham gia. Trong sheet, không thêm ý tưởng, không tích "
        "Chưa có ý tưởng, bấm nút Xin tham gia ở cuối. Không gửi gì thêm. "
        "Cuối cùng: báo dòng lỗi dưới mục ý tưởng, và sheet còn mở hay không. Sau đó bấm Hủy.",
        ["error_text", "sheet_still_open"],
    )
    assert "ý tưởng" in values["error_text"].lower()
    assert values["sheet_still_open"] is True


def test_tc10_tabs(run_case):
    values = run_case(
        "TC-10",
        "Bắt đầu: tab Trang chủ. Bấm lần lượt từng tab ở thanh dưới, từ trái sang phải, đợi mỗi màn tải xong. "
        "Cuối cùng: báo danh sách tên tab theo thứ tự và có màn nào hiện lỗi hay không.",
        ["tabs", "error_shown"],
    )
    assert len(values["tabs"]) == 5
    assert "Tin nhắn" in values["tabs"], "MB-62: tab Tin nhắn thay tab Hồ sơ"
    assert values["error_shown"] is False


@pytest.mark.pending("sinh viên đã có nhóm")
def test_tc11_lecturer_detail(run_case):
    values = run_case(
        "TC-11",
        "Bắt đầu: tab Nhóm của sinh viên đã có nhóm. Mở danh sách giảng viên, mở giảng viên đầu tiên. "
        "Cuối cùng: báo tên giảng viên và ít nhất một lĩnh vực chuyên môn đang hiện.",
        ["lecturer_name", "expertise"],
    )
    assert values["lecturer_name"]
    assert values["expertise"]


def test_tc14_remote_config_on_screen(run_case):
    values = run_case(
        "TC-14",
        "Bắt đầu: tab Trang chủ. Bấm ảnh đại diện chữ cái ở góc phải trên để mở Hồ sơ của tôi, bấm nút Cài đặt, cuộn xuống mục Demo Firebase. "
        "Cuối cùng: báo số ở dòng 'Số nhóm gợi ý tối đa' dưới dạng số nguyên.",
        ["suggestions_max_items"],
    )
    assert int(values["suggestions_max_items"]) == int(os.environ.get("EXPECTED_SUGGESTIONS_MAX", "5"))


def test_tc17_sign_out(run_case):
    values = run_case(
        "TC-17",
        "Bắt đầu: tab Trang chủ. Bấm ảnh đại diện chữ cái ở góc phải trên để mở Hồ sơ của tôi, bấm nút Cài đặt, bấm Đăng xuất, xác nhận Đăng xuất ở hộp thoại. "
        "Cuối cùng: báo màn đăng nhập có hiện hay không. Ca này chạy cuối, đăng nhập lại bằng tay sau khi xong.",
        ["login_screen_shown"],
    )
    assert values["login_screen_shown"] is True
