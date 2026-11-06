import { Banner, Button, Dialog, StatusChip } from '@/shared/components/ui'
import { lecturerReasonLabel } from '@/features/my_team/advisorsApi'
import type { AdvisorMatchingRun } from './api'

/**
 * Dialog xem trước kết quả "Ghép sớm" (W19, W28): số nhóm ghép được / chưa ghép được và từng dòng nhóm → cố vấn · lý do.
 * "Chốt kết quả" áp dụng; "Bỏ kết quả" hủy lần xem trước; Esc chỉ đóng (lần xem trước vẫn mở lại được).
 * Mặc định cho giảng viên; W28 truyền `advisorNoun="mentor"` và `reasonLabel={mentorReasonLabel}`.
 */
export function EarlyMatchingDialog({
  run,
  onClose,
  onConfirm,
  onDiscard,
  confirming,
  discarding,
  error,
  advisorNoun = 'giảng viên',
  reasonLabel = lecturerReasonLabel,
}: {
  run: AdvisorMatchingRun | null
  onClose: () => void
  onConfirm: () => void
  onDiscard: () => void
  confirming: boolean
  discarding: boolean
  error?: { title: string; detail?: string } | null
  advisorNoun?: string
  reasonLabel?: (reason?: string | null, rank?: number | null) => string | undefined
}) {
  const plan = run?.plan ?? []
  const busy = confirming || discarding
  return (
    <Dialog
      open={!!run}
      onClose={onClose}
      title="Chốt kết quả ghép sớm?"
      actions={
        <>
          <Button variant="text" onClick={onDiscard} loading={discarding} disabled={busy}>
            Bỏ kết quả
          </Button>
          <Button onClick={onConfirm} loading={confirming} disabled={busy || plan.length === 0}>
            Chốt kết quả
          </Button>
        </>
      }
    >
      {run && (
        <div className="flex flex-col gap-3">
          <p>
            Ghép được <strong className="text-text-primary">{run.matchedCount} nhóm</strong>
            {run.unmatchedCount > 0 && (
              <>
                {' '}· <strong className="text-warning">{run.unmatchedCount} nhóm</strong> chưa có {advisorNoun} còn chỗ
              </>
            )}
            . {advisorNoun === 'giảng viên' ? 'Giảng viên' : 'Mentor'} và nhóm được báo ngay sau khi chốt.
          </p>
          {error && (
            <Banner tone="error" title={error.title}>
              {error.detail}
            </Banner>
          )}
          {plan.length === 0 ? (
            <p>Không có nhóm nào cần ghép.</p>
          ) : (
            <ul aria-label="Kết quả xem trước" className="max-h-[320px] overflow-y-auto rounded-md border border-border">
              {plan.map((item) => (
                <li key={item.teamId} className="flex flex-wrap items-center gap-x-3 gap-y-1 border-b border-border px-3 py-2 last:border-b-0">
                  <span className="min-w-0 flex-1 font-medium text-text-primary">{item.teamName}</span>
                  {item.advisorId ? (
                    <>
                      <span className="text-text-primary">{item.advisorName}</span>
                      <StatusChip tone="info">{reasonLabel(item.reason, item.preferenceRank) ?? 'Hệ thống ghép'}</StatusChip>
                    </>
                  ) : (
                    <StatusChip tone="warning">Chưa có {advisorNoun}</StatusChip>
                  )}
                </li>
              ))}
            </ul>
          )}
        </div>
      )}
    </Dialog>
  )
}
