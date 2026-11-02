# BE-73 – Ghi chú tích hợp

- Producer `TopicInterestOpened` gửi `memberStudentIds` của nhóm, dùng Outbox và camelCase theo `docs/contracts/events.md`. Notification consumer giữ nguyên.
- Giới hạn giảng viên: sức chứa còn lại theo học kỳ/môn + số dự phòng từ Academic; không đặt tối đa thì dùng cấu hình mặc định 10. Mentor: tối đa + số dự phòng, không trừ số nhóm đã nhận. Quan tâm của mentor được đếm chung hai môn trong học kỳ.
- BE-71 được gọi qua `ITopicSuggestionClient`; không thay bằng danh sách nhóm cục bộ khi Matching lỗi.

## Điểm bàn giao cho luồng ghép cố vấn của Thiên

`ITopicInterestService.CloseInterestsOnAssignmentAsync(teamId, advisorKind, matchedAdvisorId, ct)` đóng các quan tâm OPEN cùng loại và phát `TopicInterestClosed` qua Outbox. Người được ghép nhận reason `THIS_ADVISOR_MATCHED`, các cố vấn còn lại nhận `OTHER_ADVISOR_MATCHED`. Quan tâm đã rút và quan tâm khác loại được giữ nguyên; gọi lại không phát trùng sự kiện.

Luồng lưu kết quả ghép tự động, ghép sớm và thay cố vấn thủ công cần gọi hàm trên với `LECTURER` hoặc `MENTOR`. Ghi assignment và đóng quan tâm phải nằm trong cùng transaction: hàm đóng quan tâm có gọi SaveChanges, nên caller cần mở transaction trước khi ghi assignment và chỉ commit sau khi cả hai bước thành công.

Tại `origin/develop` f321a42, module hiện chỉ có API đọc assignment; chưa có luồng ghi kết quả ghép để nối lời gọi này. Vì vậy chưa nghiệm thu tự đóng quan tâm từ thao tác ghép thực tế. Đây là điểm tích hợp với module ghép của Thiên, không phải BE-73 phải chờ BE-71 chưa hoàn thành.

## Mobile

MB-27 đã merge qua PR #72. Note review của Thiên ghi `getTopicInterests` và `teamTopicInterestsProvider` chưa được màn nào dùng; cần kiểm thử liên thông sau khi API BE-73 được tích hợp. PR #75 cần xử lý conflict điều hướng S15 → S25 → S23 theo note của Thiên.
