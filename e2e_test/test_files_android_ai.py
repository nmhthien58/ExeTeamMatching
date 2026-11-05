"""Xuất file, tương tác Android gốc, tính năng AI."""

import pytest


@pytest.mark.pending("MB-53")
def test_tc12_export_report(run_case):
    values = run_case(
        "TC-12",
        "Bắt đầu: màn Dự án của sinh viên đã có nhóm. Cuộn tới thẻ Báo cáo dự án, bấm Xuất báo cáo PDF, đợi xong. "
        "Cuối cùng: báo dòng kết quả, và có nút Mở báo cáo hay không.",
        ["result_text", "open_button_shown"],
    )
    assert values["result_text"] == "Đã lưu báo cáo"
    assert values["open_button_shown"] is True


@pytest.mark.pending("MB-53")
def test_tc13_export_without_network(run_case):
    values = run_case(
        "TC-13",
        "Bắt đầu: màn Dự án, điện thoại đã bật chế độ máy bay. Bấm Xuất báo cáo PDF, đợi xong. "
        "Cuối cùng: báo dòng lỗi, nhãn của nút và app có bị đóng hay không. Sau đó tắt chế độ máy bay.",
        ["error_text", "button_label", "app_closed"],
    )
    assert "Mất kết nối" in values["error_text"]
    assert values["button_label"] == "Thử lại"
    assert values["app_closed"] is False


def test_tc15_system_back(run_case):
    values = run_case(
        "TC-15",
        "Bắt đầu: tab Khám phá. Mở nhóm đầu tiên, rồi bấm nút Back của hệ thống Android, không bấm mũi tên trong app. "
        "Cuối cùng: báo tab đang chọn và app còn mở hay không.",
        ["selected_tab", "app_open"],
    )
    assert values["selected_tab"] == "Khám phá"
    assert values["app_open"] is True


@pytest.mark.pending("MB-52")
def test_tc16_notification_permission(run_case):
    values = run_case(
        "TC-16",
        "Bắt đầu: vừa cài app trên Android 13 trở lên. Mở app, ở hộp xin quyền thông báo của hệ thống bấm Cho phép. "
        "Cuối cùng: báo hộp xin quyền còn hiện hay không.",
        ["permission_dialog_shown"],
    )
    assert values["permission_dialog_shown"] is False


@pytest.mark.pending("MB-56")
def test_tc19_ai_suggestion(run_case):
    values = run_case(
        "TC-19",
        "Bắt đầu: màn chi tiết một nhóm, cờ ai_idea_enabled đang bật. Bấm Xin tham gia, Gợi ý bằng AI, nhập "
        "'Ứng dụng giúp sinh viên đặt lịch dùng chung phòng họp', bấm Tạo gợi ý, đợi xong, bấm Dùng gợi ý. "
        "Cuối cùng: báo tiêu đề ý tưởng đã điền và có nhãn 'Gợi ý của AI' hay không. Sau đó bấm Hủy.",
        ["idea_title", "ai_label_shown"],
    )
    assert values["idea_title"]
    assert values["ai_label_shown"] is True


@pytest.mark.pending("MB-56")
def test_tc20_ai_flag_off(run_case):
    values = run_case(
        "TC-20",
        "Bắt đầu: màn chi tiết một nhóm, cờ ai_idea_enabled đang tắt. Bấm Xin tham gia, bấm Thêm ý tưởng, gõ 'Ý tưởng thử'. "
        "Cuối cùng: báo nút Gợi ý bằng AI có hiện hay không và nội dung ô ý tưởng. Sau đó bấm Hủy.",
        ["ai_button_shown", "idea_value"],
    )
    assert values["ai_button_shown"] is False
    assert values["idea_value"] == "Ý tưởng thử"
