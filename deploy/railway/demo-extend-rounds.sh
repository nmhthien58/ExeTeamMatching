#!/usr/bin/env bash
# Lùi hạn chót các đợt đang mở (OPEN) cho buổi demo, kèm giờ chạy job tự đóng đợt trong Hangfire.
# API không cho sửa hạn chót của đợt đang mở (Job đã hẹn giờ theo hạn cũ) nên sửa thẳng 2 chỗ: academic_db và job_db.
#   bash deploy/railway/demo-extend-rounds.sh 2026-11-12T17:00:00Z
# Không xóa Redis toàn bộ (giữ read model của Matching), chỉ xóa cache khác "matching:*" để Academic/Team đọc hạn mới.
set -euo pipefail

DEADLINE="${1:?Cần hạn chót mới, ví dụ 2026-11-12T17:00:00Z}"

run() {
  local b
  b=$(printf '%s' "$2" | base64 -w0)
  railway ssh -s postgres -- sh -c "echo $b | base64 -d | psql -U postgres -d $1 -At -v ON_ERROR_STOP=1"
}

echo "1/3 Đợt đang mở trong academic_db → hạn $DEADLINE"
IDS=$(run academic_db "update academic.registration_rounds set deadline_at = '$DEADLINE', updated_at = now()
  where status = 'OPEN' returning id;" | grep -E '^[0-9a-f-]{36}$' || true)
[ -n "$IDS" ] || { echo "Không có đợt nào đang mở"; exit 0; }
echo "$IDS"

echo "2/3 Job CloseAsync của các đợt đó trong Hangfire → chạy lúc $DEADLINE"
LIST=$(echo "$IDS" | sed "s/.*/'&'/" | paste -sd, -)
run job_db "update hangfire.set s set score = extract(epoch from timestamptz '$DEADLINE')
  from hangfire.job j
  where s.key = 'schedule' and s.value = j.id::text and j.statename = 'Scheduled'
    and j.invocationdata::json->>'Method' = 'CloseAsync'
    and trim(both '\"' from j.arguments::json->>0) in ($LIST)
  returning j.id, to_timestamp(s.score);"

echo "3/3 Xóa cache (trừ matching:*)"
railway ssh -s Redis -- sh -c 'redis-cli -a "$REDIS_PASSWORD" --no-auth-warning --scan | grep -v "^matching:" | xargs -r redis-cli -a "$REDIS_PASSWORD" --no-auth-warning del' \
  || echo "Không xóa được cache (tự hết hạn sau ít phút)."
echo "Xong."
