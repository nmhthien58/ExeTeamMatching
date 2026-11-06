# Kết quả SonarCloud theo PR (MB-59)

Lấy ngày 05/11/2026 qua Web API của SonarCloud (`api/issues/search` và `api/hotspots/search`, tham số `pullRequest`), project `nmhthien58_ExeTeamMatching`. Quality gate của mọi PR dưới đây đều **Passed** lúc merge, trừ #154 (Failed, Security Rating E) và lần chạy đầu của #161 (Failed, Security Rating E).

| PR | Task | Loại | Mức | Trạng thái | Rule | File:dòng | Thông điệp |
|---|---|---|---|---|---|---|---|
| #154 | BE-85 | Vulnerability | Blocker | Open | docker:S6472 | web/Dockerfile:10 | Make sure that using ARG to handle a secret is safe here. |
| #155 | BE-85 | – | – | – | – | – | Không có issue |
| #157 | MB-24 | – | – | – | – | – | Không có issue |
| #161 | BE-85 | Vulnerability | Blocker | Closed | docker:S6472 | web/Dockerfile:15 (bản đầu của PR) | Make sure that using ENV to handle a secret is safe here. |
| #163 | BE-87 | – | – | – | – | – | Không có issue |
| #164 | MB-61 | – | – | – | – | – | Không có issue |
| #165 | FE-50 | Code smell | Major | Open | typescript:S6845 | web/src/features/client-errors/ClientErrorDrawer.tsx:40 | `tabIndex` should only be declared on interactive elements. |
| #165 | FE-50 | Code smell | Major | Open | typescript:S6845 | web/src/features/client-errors/DailyErrorChart.tsx:44 | `tabIndex` should only be declared on interactive elements. |
| #166 | BE-84 | – | – | – | – | – | Không có issue |
| #167 | MB-53 | – | – | – | – | – | Không có issue |
| #168 | MB-56 | – | – | – | – | – | Không có issue |
| #169 | MB-55 | – | – | – | – | – | Không có issue |
| #170 | FE-48 | – | – | – | – | – | Không có issue |
| #171 | MB-54 | – | – | – | – | – | Không có issue |
| #172 | MB-52 | – | – | – | – | – | Không có issue |
| #173 | MB-58 | – | – | – | – | – | Không có issue |
| #174 | MB-56 | – | – | – | – | – | Không có issue |

Ghi chú: SonarCloud gói miễn phí không phân tích Dart, nên với code Flutter, phần review bằng AI do Claude Code đọc diff PR (CR-05 đến CR-08 trong `docs/code-review.md`).
