import { useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import type { ColumnDef } from '@tanstack/react-table'
import { toApiError } from '@/shared/api/apiClient'
import { DataTable } from '@/shared/components/DataTable'
import { EmptyState } from '@/shared/components/States'
import { Button, Dialog, StatusChip } from '@/shared/components/ui'
import { PageHeader } from '@/shared/layout/PageHeader'
import { autoAssignKeys, exportTeams, fetchTeams, useFinalizeTeam } from '@/features/auto-assign/api'
import { AutoAssignTabs } from '@/features/auto-assign/AutoAssignTabs'
import { useCourseParam, useCurrentSemester } from '@/features/dashboard/adminApi'
import { CourseToggle } from '@/features/dashboard/CourseToggle'
import type { TeamCard } from '@/features/suggestions/api'
import { useSnackbar } from '@/features/workspace/useSnackbar'

type View = 'NEEDS_REVIEW' | 'FINALIZED'

const errorText: Record<string, string> = {
  'composition-invalid': 'Nhóm chưa đủ sĩ số hoặc số chuyên ngành theo quy tắc. Thêm hoặc chuyển thành viên ở Kết quả ghép rồi chốt lại.',
}

/** Vấn đề của nhóm theo quy tắc 1, 2 (đọc từ requirement của TeamCard). */
function problems(team: TeamCard): string[] {
  const r = team.requirement
  if (!r || r.isValid) return []
  const list: string[] = []
  if (r.memberCount < r.minSize) list.push(`Thiếu ${r.minSize - r.memberCount} người`)
  if (r.memberCount > r.maxSize) list.push(`Quá ${r.memberCount - r.maxSize} người`)
  if (r.specializationCount < r.minSpecializations) list.push('Thiếu chuyên ngành thứ hai')
  return list
}

/**
 * W22 – Chốt danh sách và xuất file (FE-28, rule 13): hệ thống đã tự chốt nhóm hợp lệ; trang này liệt kê nhóm Cần xem lại,
 * cho chốt hoặc mở lại từng nhóm, xuất Excel danh sách cuối. Trạng thái: còn nhóm cần xem lại; đã chốt hết.
 */
export function FinalizePage() {
  const [course, setCourse] = useCourseParam()
  const semester = useCurrentSemester()
  const [params, setParams] = useSearchParams()
  const view: View = params.get('trang-thai') === 'FINALIZED' ? 'FINALIZED' : 'NEEDS_REVIEW'
  const finalize = useFinalizeTeam(course)
  const snackbar = useSnackbar()
  const [confirm, setConfirm] = useState<TeamCard | null>(null)
  const [exporting, setExporting] = useState(false)
  const [exportError, setExportError] = useState<string | null>(null)

  const setView = (next: View) => {
    const p = new URLSearchParams(params)
    p.set('trang-thai', next)
    p.delete('page')
    setParams(p, { replace: true })
  }

  const download = async () => {
    setExporting(true)
    setExportError(null)
    try {
      await exportTeams(semester.data?.id, course)
    } catch (e) {
      setExportError(toApiError(e).title)
    } finally {
      setExporting(false)
    }
  }

  const columns: ColumnDef<TeamCard, unknown>[] = [
    {
      header: 'Nhóm',
      id: 'name',
      meta: { sortKey: 'name' },
      cell: ({ row }) => (
        <span className="flex flex-col">
          <span className="font-medium">{row.original.name}</span>
          {row.original.projectName && <span className="text-text-secondary">{row.original.projectName}</span>}
        </span>
      ),
    },
    { header: 'Thành viên', id: 'members', meta: { align: 'right' }, cell: ({ row }) => `${row.original.memberCount}/6` },
    {
      header: 'Điều kiện',
      id: 'requirement',
      meta: { minWidth: 240 },
      cell: ({ row }) => {
        const list = problems(row.original)
        return list.length === 0 ? (
          <StatusChip tone="success">Đủ điều kiện</StatusChip>
        ) : (
          <StatusChip tone="warning" icon="error">
            {list.join(' · ')}
          </StatusChip>
        )
      },
    },
    { header: 'GV', id: 'lecturer', cell: ({ row }) => row.original.lecturerName ?? '–' },
    {
      header: '',
      id: 'actions',
      meta: { align: 'right' },
      cell: ({ row }) => (
        <Button variant={view === 'NEEDS_REVIEW' ? 'outline' : 'text'} onClick={() => setConfirm(row.original)}>
          {view === 'NEEDS_REVIEW' ? 'Chốt' : 'Mở lại'}
        </Button>
      ),
    },
  ]

  const action = view === 'NEEDS_REVIEW' ? 'finalize' : 'reopen'
  const apiError = finalize.error ? toApiError(finalize.error) : null

  return (
    <>
      <PageHeader
        title="Chốt danh sách"
        action={
          <Button icon="download" loading={exporting} onClick={() => void download()}>
            Xuất Excel
          </Button>
        }
      />
      <AutoAssignTabs />
      <div className="mb-4 flex flex-wrap items-center gap-3">
        <CourseToggle value={course} onChange={setCourse} />
        <div role="tablist" aria-label="Trạng thái nhóm" className="ml-auto flex gap-2">
          {(['NEEDS_REVIEW', 'FINALIZED'] as View[]).map((v) => (
            <Button key={v} role="tab" aria-selected={view === v} variant={view === v ? 'outline' : 'text'} onClick={() => setView(v)}>
              {v === 'NEEDS_REVIEW' ? 'Cần xem lại' : 'Đã chốt'}
            </Button>
          ))}
        </div>
      </div>
      {exportError && <p role="alert" className="mb-3 text-error">{exportError}</p>}

      <DataTable
        queryKey={[...autoAssignKeys.teams(course), view]}
        fetchPage={fetchTeams(course, view)}
        columns={columns}
        getRowId={(t) => t.id}
        searchPlaceholder="Tên nhóm hoặc đề tài"
        empty={
          view === 'NEEDS_REVIEW' ? (
            <EmptyState
              icon="verified"
              title="Đã chốt hết nhóm"
              description="Không còn nhóm nào cần xem lại. Xuất Excel để lưu danh sách cuối."
              action={<Button variant="outline" icon="download" onClick={() => void download()}>Xuất Excel</Button>}
            />
          ) : (
            <EmptyState icon="inbox" title="Chưa có nhóm nào được chốt" />
          )
        }
      />

      <Dialog
        open={!!confirm}
        onClose={() => {
          setConfirm(null)
          finalize.reset()
        }}
        title={action === 'finalize' ? `Chốt nhóm ${confirm?.name}?` : `Mở lại nhóm ${confirm?.name}?`}
        actions={
          <>
            <Button variant="text" onClick={() => setConfirm(null)}>
              Hủy
            </Button>
            <Button
              loading={finalize.isPending}
              onClick={() =>
                finalize.mutate(
                  { teamId: confirm!.id, action },
                  {
                    onSuccess: () => {
                      snackbar.show(action === 'finalize' ? 'Đã chốt nhóm' : 'Đã mở lại nhóm')
                      setConfirm(null)
                    },
                  },
                )
              }
            >
              {action === 'finalize' ? 'Chốt nhóm' : 'Mở lại'}
            </Button>
          </>
        }
      >
        {action === 'finalize'
          ? 'Nhóm được chốt sẽ không đổi thành viên được nữa; thành viên nhận thông báo.'
          : 'Nhóm trở lại trạng thái Cần xem lại để Bộ môn chỉnh sửa, rồi phải chốt lại.'}
        {apiError && <p role="alert" className="mt-2 text-error">{errorText[apiError.code] ?? apiError.detail ?? apiError.title}</p>}
      </Dialog>
      {snackbar.element}
    </>
  )
}
