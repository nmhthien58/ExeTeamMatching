# Đề xuất: bỏ phiếu đổi nhóm trưởng (BE-88, MB-63, FE-54)

> 06/11/2026 – Thiên. Trạng thái: **đề xuất, chờ Cường xem** (Team Service – module Members là vùng của Cường; đổi DB script và sự kiện Kafka cần Thiên duyệt theo AGENTS.md mục 4).

## 1. Vấn đề

Người tạo nhóm tự thành nhóm trưởng. Nhóm trưởng **đã chuyển quyền được** (`POST /api/teams/{teamId}/transfer-leader`, sự kiện `LeaderChanged`, có trên mobile và web). Nhưng nếu nhóm trưởng không làm việc hoặc cả nhóm muốn người khác dẫn dắt thì các thành viên không có cách nào đổi, ngoài nhờ chính nhóm trưởng.

## 2. Giải pháp: kết hợp hai cách

| Cách | Ai bắt đầu | Điều kiện | Kết quả |
|---|---|---|---|
| **Chuyển quyền** (đã có) | Nhóm trưởng | Chọn một thành viên | Đổi ngay |
| **Đề nghị đổi nhóm trưởng** (mới) | Bất kỳ thành viên | Đề cử một thành viên (có thể là chính mình), nhóm bỏ phiếu | Quá nửa **số thành viên** đồng ý → đổi; hết hạn chưa đủ → hủy |

Nhóm **luôn có đúng một nhóm trưởng** trong suốt quá trình (quy tắc 1 không đổi), nên các luồng đang dựa vào nhóm trưởng (duyệt yêu cầu tham gia, nguyện vọng giảng viên, đặt lịch họp, yêu cầu đổi nhóm) không bị ảnh hưởng.

## 3. Quy tắc

1. Mỗi nhóm chỉ có **một đề nghị đang mở**.
2. Người đề nghị **tự động tính một phiếu đồng ý**. Người được đề cử và nhóm trưởng hiện tại cũng được bỏ phiếu.
3. **Đủ phiếu**: số phiếu đồng ý > số thành viên / 2 (nhóm 4 người cần 3, nhóm 5 cần 3, nhóm 6 cần 4). Đủ là chuyển ngay, không chờ hết hạn.
4. **Không thể đủ** (số phiếu chưa bỏ + số đồng ý ≤ một nửa) → đóng sớm là "Không thông qua".
5. **Hạn 48 giờ** từ lúc mở; hết hạn chưa đủ → "Hết hạn". Job Service đóng đề nghị hết hạn (hoặc Team Service kiểm tra lúc đọc).
6. **Được đổi phiếu** khi đề nghị còn mở.
7. Người được đề cử **rời nhóm** hoặc nhóm trưởng **tự chuyển quyền** trong lúc bỏ phiếu → đề nghị bị **hủy**.
8. **Không mở được** khi nhóm `Finalized` hoặc nhóm EXE201 kế thừa (đổi nhóm trưởng EXE201 đi qua yêu cầu đổi nhóm – `SET_LEADER`, quy tắc của đợt đổi nhóm), hoặc nhóm chỉ có 1 thành viên.
9. Người đề nghị **rút** được đề nghị khi còn mở.

## 4. Database (`team_db`, cần sửa `deploy/db/03_team_db.sql` + PDF thiết kế DB)

```sql
CREATE TABLE team.leader_votes (
    id                     uuid         PRIMARY KEY,
    team_id                uuid         NOT NULL REFERENCES team.teams(id),
    proposed_by_student_id uuid         NOT NULL,
    nominee_student_id     uuid         NOT NULL,
    reason                 varchar(300),
    status                 varchar(16)  NOT NULL DEFAULT 'OPEN',
    expires_at             timestamptz  NOT NULL,
    created_at             timestamptz  NOT NULL DEFAULT now(),
    closed_at              timestamptz,
    CONSTRAINT ck_leader_votes_status CHECK (status IN ('OPEN', 'PASSED', 'REJECTED', 'EXPIRED', 'CANCELLED'))
);
CREATE UNIQUE INDEX uq_leader_votes_one_open ON team.leader_votes (team_id) WHERE status = 'OPEN';

CREATE TABLE team.leader_vote_ballots (
    vote_id    uuid        NOT NULL REFERENCES team.leader_votes(id) ON DELETE CASCADE,
    student_id uuid        NOT NULL,
    approve    boolean     NOT NULL,
    voted_at   timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (vote_id, student_id)
);
```

Khi thông qua: trong **cùng transaction** đổi `team_members.role` (giống `TransferLeaderAsync`), ghi `audit_log` (`LEADER_CHANGED`, actor = "Bỏ phiếu"), ghi Outbox `LeaderChanged`.

## 5. API (Team Service, qua Gateway)

| Method | Route | Ai | Mô tả |
|---|---|---|---|
| `GET` | `/api/teams/{teamId}/leader-votes/current` | Thành viên | Đề nghị đang mở (hoặc 204), số phiếu, phiếu của tôi, hạn |
| `POST` | `/api/teams/{teamId}/leader-votes` | Thành viên | Mở đề nghị `{ nomineeStudentId, reason? }` → 201; đã có đề nghị mở → 409 ProblemDetails |
| `PUT` | `/api/teams/{teamId}/leader-votes/{voteId}/ballot` | Thành viên | Bỏ / đổi phiếu `{ approve }` → trả đề nghị sau khi đếm (có thể đã `PASSED`) |
| `DELETE` | `/api/teams/{teamId}/leader-votes/{voteId}` | Người đề nghị | Rút đề nghị → `CANCELLED` |

Code theo cấu trúc module: `Team.Api/Controllers/LeaderVotes`, `Team.Services/LeaderVotes`, `Team.Repositories/LeaderVotes`.

## 6. Sự kiện Kafka (topic `exe.team`, cần Thiên duyệt)

| Sự kiện | Khi | Notification Service |
|---|---|---|
| `LeaderVoteOpened` | Mở đề nghị | Báo mọi thành viên (trừ người đề nghị): "An đề nghị Bình làm nhóm trưởng – bỏ phiếu trước 18:00 · 08/11" |
| `LeaderVoteClosed` (`status`) | Thông qua / không thông qua / hết hạn / hủy | Báo cả nhóm kết quả |
| `LeaderChanged` (đã có) | Thông qua | Như hiện tại; Collaboration cập nhật vai trò trong workspace |

## 7. Giao diện

**Mobile S16 (Thành viên) – Cường; web cùng mã màn hình.**
- Menu "⋮" trên dòng một thành viên (không phải nhóm trưởng): mục **"Đề nghị làm nhóm trưởng"** (thành viên thường) bên cạnh **"Chuyển quyền nhóm trưởng"** (đã có, chỉ nhóm trưởng thấy).
- Bottom sheet: người được đề cử, ô lý do (≤ 300 ký tự, không bắt buộc), dòng giải thích "Cần 3/5 thành viên đồng ý trong 48 giờ", nút **"Gửi đề nghị"**.
- **Thẻ đề nghị** ở đầu S15 (Nhóm của tôi) khi có đề nghị mở: "An đề nghị **Bình** làm nhóm trưởng", thanh tiến độ phiếu (2/3 cần), hạn còn lại, hai nút **"Đồng ý"** / **"Không đồng ý"** (đã bỏ phiếu thì hiện lựa chọn và "Đổi phiếu"); người đề nghị có "Rút đề nghị".
- Kết quả hiện SnackBar và thông báo A06. Trạng thái dùng chip chuẩn `DESIGN.md` mục 7 (Đang chờ / Đã duyệt / Đã khóa).

## 8. Kiểm thử cần có

- Service: đủ phiếu → đổi nhóm trưởng + `LeaderChanged` trong cùng transaction; không thể đủ → `REJECTED`; hết hạn → `EXPIRED`; đề cử rời nhóm → `CANCELLED`; mở lần hai → 409; nhóm `Finalized` / EXE201 → 409.
- Mobile/web: thẻ đề nghị, bỏ phiếu, đổi phiếu, rút đề nghị.

## 9. Ước lượng

Backend khoảng 1 ngày (bảng, 4 API, 2 sự kiện, job hết hạn, test). Mobile + web khoảng 1 ngày. **Không ưu tiên trước hạn PRM393** – làm khi đã xong bài nộp.
