import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import type { ColumnDef } from '@tanstack/react-table'
import { toApiError } from '@/shared/api/apiClient'
import { DataTable } from '@/shared/components/DataTable'
import { EmptyState } from '@/shared/components/States'
import { Button, Dialog, StatusChip } from '@/shared/components/ui'
import { PageHeader } from '@/shared/layout/PageHeader'
import { useIsTeamFormation } from '@/shared/phase/usePhase'
import { autoAssignKeys, fetchUnmatched, useRemindUnmatched, useRunAutoAssign } from '@/features/auto-assign/api'
import { AutoAssignTabs } from '@/features/auto-assign/AutoAssignTabs'
import { useCourseParam, useCurrentSemester } from '@/features/dashboard/adminApi'
import { CourseToggle } from '@/features/dashboard/CourseToggle'
import { useSpecializations, type StudentRef } from '@/features/suggestions/api'
import { useSnackbar } from '@/features/workspace/useSnackbar'
import { useTeamRules } from '@/shared/lib/teamRules'

const columns: ColumnDef<StudentRef, unknown>[] = [
  { header: 'MSSV', accessorKey: 'studentCode', meta: { sortKey: 'studentCode' } },
  { header: 'Họ tên', accessorKey: 'fullName', meta: { sortKey: 'name' } },
  { header: 'Chuyên ngành', id: 'specialization', cell: ({ row }) => row.original.specialization?.name ?? '–' },
]

/**
 * W20 – Sinh viên chưa có nhóm (FE-26): danh sách lọc theo chuyên ngành, gửi nhắc; sau hạn chót hệ thống tự ghép,
 * Admin chạy lại được. Trạng thái: có dữ liệu; không còn sinh viên lẻ.
 */
export function UnmatchedPage() {
  const rules = useTeamRules()
  const [course, setCourse] = useCourseParam()
  const semester = useCurrentSemester()
  const { isTeamFormation, phase } = useIsTeamFormation(course)
  const specializations = useSpecializations()
  const remind = useRemindUnmatched()
  const run = useRunAutoAssign(semester.data?.id, course)
  const navigate = useNavigate()
  const snackbar = useSnackbar()
  const [dialog, setDialog] = useState<'remind' | 'run' | null>(null)
  const afterDeadline = !!phase && !isTeamFormation

  return (
    <>
      <PageHeader
        title="Sinh viên chưa có nhóm"
        action={
          afterDeadline && (
            <Button icon="auto_awesome" onClick={() => setDialog('run')}>
              Chạy lại ghép tự động
            </Button>
          )
        }
      />
      <AutoAssignTabs />
      <div className="mb-4">
        <CourseToggle value={course} onChange={setCourse} />
      </div>

      <DataTable
        queryKey={autoAssignKeys.unmatched(course)}
        fetchPage={fetchUnmatched(course)}
        columns={columns}
        getRowId={(s) => s.id}
        searchPlaceholder="Tên hoặc MSSV"
        filters={[
          {
            key: 'specializationId',
            label: 'Chuyên ngành',
            options: (specializations.data ?? []).map((s) => ({ value: String(s.id), label: s.name })),
          },
        ]}
        toolbarRight={
          <Button variant="outline" icon="notifications" onClick={() => setDialog('remind')}>
            Gửi nhắc
          </Button>
        }
        empty={
          <EmptyState
            icon="task_alt"
            title="Không còn sinh viên lẻ"
            description={`Mọi sinh viên ${course} đã có nhóm.`}
            action={<StatusChip tone="success">Đủ điều kiện</StatusChip>}
          />
        }
      />

      <Dialog
        open={dialog === 'remind'}
        onClose={() => setDialog(null)}
        title="Gửi nhắc sinh viên chưa có nhóm?"
        actions={
          <>
            <Button variant="text" onClick={() => setDialog(null)}>
              Hủy
            </Button>
            <Button
              loading={remind.isPending}
              onClick={() =>
                remind.mutate(course, {
                  onSuccess: () => {
                    setDialog(null)
                    snackbar.show('Đã gửi nhắc')
                  },
                })
              }
            >
              Gửi nhắc
            </Button>
          </>
        }
      >
        Mỗi sinh viên {course} chưa có nhóm nhận một email và thông báo trong ứng dụng.
        {remind.error && <p className="mt-2 text-error">{toApiError(remind.error).title}</p>}
      </Dialog>

      <Dialog
        open={dialog === 'run'}
        onClose={() => setDialog(null)}
        title="Chạy lại ghép tự động?"
        actions={
          <>
            <Button variant="text" onClick={() => setDialog(null)}>
              Hủy
            </Button>
            <Button loading={run.isPending} onClick={() => run.mutate(undefined, { onSuccess: () => navigate(`/admin/chua-co-nhom/ket-qua?mon=${course}`) })}>
              Chạy lại
            </Button>
          </>
        }
      >
        Sinh viên còn lẻ được bổ sung vào nhóm cần xem lại, phần còn lại lập nhóm mới {rules.minSize}–{rules.maxSize} người. Sinh viên và nhóm được thông báo ngay.
        {run.error && <p className="mt-2 text-error">{toApiError(run.error).detail ?? toApiError(run.error).title}</p>}
      </Dialog>
      {snackbar.element}
    </>
  )
}
