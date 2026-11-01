import { useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { toApiError } from '@/shared/api/apiClient'
import { EmptyState, ErrorState, TableSkeleton } from '@/shared/components/States'
import { Button, Dialog, StatusChip, type StatusTone } from '@/shared/components/ui'
import type { CourseCode } from '@/shared/phase/usePhase'
import { COURSES } from '@/features/dashboard/adminApi'
import { useSemesterList } from '@/features/semesters/api'
import { inputClass } from '@/features/semesters/Field'
import { useChangeRoundState, useRounds, type Round, type RoundStatus } from './api'
import { roundTypeLabels, type RoundFormTarget } from './RoundForm'

const statuses: Record<RoundStatus, { label: string; tone: StatusTone }> = {
  SCHEDULED: { label: 'Chưa mở', tone: 'neutral' },
  OPEN: { label: 'Đang mở', tone: 'success' },
  CLOSED: { label: 'Đã khóa', tone: 'warning' },
}

function formatTime(value: string) {
  return new Intl.DateTimeFormat('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' }).format(new Date(value))
}

const th = 'h-11 px-4 text-left text-caption font-semibold text-text-secondary'

/** W04 – đợt đăng ký của một học kỳ (`?hk=`, mặc định học kỳ hiện hành); mở / khóa thủ công có dialog xác nhận vì không quay lại được. */
export function RoundsTab({ onCreate, onEdit }: { onCreate: (target: RoundFormTarget) => void; onEdit: (target: RoundFormTarget) => void }) {
  const semesters = useSemesterList()
  const [params, setParams] = useSearchParams()
  const semesterId = params.get('hk') ?? (semesters.data?.find((s) => s.isCurrent) ?? semesters.data?.[0])?.id ?? ''
  const [course, setCourse] = useState<CourseCode | ''>('')
  const rounds = useRounds(semesterId || undefined, course || undefined)
  const [confirm, setConfirm] = useState<{ round: Round; action: 'open' | 'close' } | null>(null)
  const change = useChangeRoundState()
  const changeError = change.error ? toApiError(change.error) : null

  const closeDialog = () => {
    setConfirm(null)
    change.reset()
  }
  const run = () => {
    if (confirm) change.mutate({ id: confirm.round.id, action: confirm.action }, { onSuccess: closeDialog })
  }

  if (semesters.isError) return <ErrorState error={semesters.error} onRetry={() => semesters.refetch()} />
  if (!semesters.isLoading && !semesters.data?.length) {
    return <EmptyState icon="calendar_month" title="Chưa có học kỳ" description="Tạo học kỳ ở tab Học kỳ trước khi mở đợt đăng ký." />
  }

  return (
    <div className="flex flex-col gap-3">
      <div className="flex flex-wrap gap-2">
        <label className="w-full sm:w-64">
          <span className="sr-only">Học kỳ</span>
          <select value={semesterId} onChange={(e) => setParams({ tab: 'dot', hk: e.target.value }, { replace: true })} className={inputClass}>
            {semesters.data?.map((s) => (
              <option key={s.id} value={s.id}>
                {s.code} – {s.name}
              </option>
            ))}
          </select>
        </label>
        <label className="w-full sm:w-44">
          <span className="sr-only">Môn</span>
          <select value={course} onChange={(e) => setCourse(e.target.value as CourseCode | '')} className={inputClass}>
            <option value="">Môn: Tất cả</option>
            {COURSES.map((c) => (
              <option key={c}>{c}</option>
            ))}
          </select>
        </label>
      </div>

      {rounds.isError && <ErrorState error={rounds.error} onRetry={() => rounds.refetch()} />}

      <div className="overflow-x-auto rounded-lg bg-surface shadow-elevation-1">
        {rounds.isLoading || semesters.isLoading ? (
          <TableSkeleton columns={6} />
        ) : !rounds.data?.length ? (
          !rounds.isError && (
            <EmptyState
              icon="event_available"
              title="Chưa có đợt đăng ký"
              description="Tạo đợt lập nhóm, đặt hạn chót rồi mở để sinh viên bắt đầu lập nhóm."
              action={<Button onClick={() => onCreate({ round: null, semesterId })}>Tạo đợt</Button>}
            />
          )
        ) : (
          <table className="w-full border-collapse text-body">
            <thead className="bg-background">
              <tr>
                <th scope="col" className={th}>Tên đợt</th>
                <th scope="col" className={th}>Môn</th>
                <th scope="col" className={th}>Loại</th>
                <th scope="col" className={th}>Giờ mở</th>
                <th scope="col" className={th}>Hạn chót</th>
                <th scope="col" className={th} style={{ minWidth: 130 }}>Trạng thái</th>
                <th scope="col" className={th}><span className="sr-only">Thao tác</span></th>
              </tr>
            </thead>
            <tbody>
              {rounds.data.map((r) => (
                <tr key={r.id} className="border-t border-border">
                  <td className="h-[52px] px-4 font-medium">{r.name}</td>
                  <td className="px-4">{r.courseCode}</td>
                  <td className="whitespace-nowrap px-4">{roundTypeLabels[r.roundType]}</td>
                  <td className="whitespace-nowrap px-4">{formatTime(r.opensAt)}</td>
                  <td className="whitespace-nowrap px-4">{formatTime(r.deadlineAt)}</td>
                  <td className="px-4">
                    <StatusChip tone={statuses[r.status].tone}>{statuses[r.status].label}</StatusChip>
                  </td>
                  <td className="whitespace-nowrap px-4 text-right">
                    {r.status !== 'CLOSED' && (
                      <Button variant="text" aria-label={`Sửa ${r.name}`} onClick={() => onEdit({ round: r })}>
                        Sửa
                      </Button>
                    )}
                    {r.status === 'SCHEDULED' && (
                      <Button variant="text" aria-label={`Mở ${r.name}`} onClick={() => setConfirm({ round: r, action: 'open' })}>
                        Mở
                      </Button>
                    )}
                    {r.status === 'OPEN' && (
                      <Button variant="text" aria-label={`Khóa ${r.name}`} onClick={() => setConfirm({ round: r, action: 'close' })}>
                        Khóa
                      </Button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {confirm && (
        <Dialog
          open
          onClose={closeDialog}
          title={confirm.action === 'open' ? 'Mở đợt đăng ký?' : 'Khóa đợt đăng ký?'}
          actions={
            <>
              <Button variant="outline" disabled={change.isPending} onClick={closeDialog}>
                Hủy
              </Button>
              <Button variant={confirm.action === 'close' ? 'danger' : 'primary'} loading={change.isPending} onClick={run}>
                {confirm.action === 'open' ? 'Mở đợt' : 'Khóa đợt'}
              </Button>
            </>
          }
        >
          <p>
            {confirm.action === 'open'
              ? `Sinh viên ${confirm.round.courseCode} thao tác được ngay. Đợt tự khóa lúc ${formatTime(confirm.round.deadlineAt)}.`
              : `Sinh viên ${confirm.round.courseCode} không thao tác được nữa. Đợt đã khóa không mở lại được.`}
          </p>
          {changeError && (
            <p role="alert" className="mt-2 text-error">
              {changeError.detail ?? changeError.title}
            </p>
          )}
        </Dialog>
      )}
    </div>
  )
}
