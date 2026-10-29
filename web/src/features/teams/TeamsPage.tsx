import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import type { ColumnDef } from '@tanstack/react-table'
import { toApiError } from '@/shared/api/apiClient'
import { DataTable } from '@/shared/components/DataTable'
import { EmptyState } from '@/shared/components/States'
import { Banner, Button, StatusChip } from '@/shared/components/ui'
import { PageHeader } from '@/shared/layout/PageHeader'
import { exportTeams } from '@/features/auto-assign/api'
import { CourseToggle } from '@/features/dashboard/CourseToggle'
import { useCourseParam, useCurrentSemester } from '@/features/dashboard/adminApi'
import { useDomains, teamStatusLabel } from '@/features/my_team/api'
import type { TeamCard } from '@/features/suggestions/api'
import { adminTeamKeys, fetchAdminTeams } from './api'

const columns: ColumnDef<TeamCard, unknown>[] = [
  { header: 'Tên nhóm', accessorKey: 'name', meta: { sortKey: 'name' } },
  { header: 'Dự án', id: 'project', cell: ({ row }) => row.original.projectName ?? '–' },
  { header: 'Domain', id: 'domains', cell: ({ row }) => row.original.domains.map((d) => d.code).join(', ') || '–' },
  {
    header: 'Thành viên',
    id: 'members',
    meta: { sortKey: 'openSlots', align: 'right' },
    cell: ({ row }) => `${row.original.memberCount}/${row.original.requirement?.maxSize ?? 6}`,
  },
  { header: 'Trưởng nhóm', id: 'leader', cell: ({ row }) => row.original.leader?.fullName ?? '–' },
  { header: 'Giảng viên', id: 'lecturer', cell: ({ row }) => row.original.lecturerName ?? 'Chưa ghép' },
  {
    header: 'Trạng thái',
    id: 'status',
    meta: { minWidth: 150 },
    cell: ({ row }) => {
      const s = teamStatusLabel[row.original.status]
      return (
        <span className="flex flex-wrap items-center gap-1">
          <StatusChip tone={s.tone} icon={s.icon}>
            {s.text}
          </StatusChip>
          {row.original.requirement && !row.original.requirement.isValid && <StatusChip tone="warning">Chưa đủ điều kiện</StatusChip>}
        </span>
      )
    },
  },
]

const statusOptions = (['DRAFT', 'ACTIVE', 'NEEDS_REVIEW', 'FINALIZED', 'DISBANDED'] as const).map((s) => ({ value: s, label: teamStatusLabel[s].text }))

/**
 * W15 – Danh sách nhóm (FE-21), giống Group List: tên nhóm, dự án, domain, số thành viên, trưởng nhóm, giảng viên, trạng thái.
 * Lọc môn, trạng thái, domain, "chưa đủ điều kiện" (quy tắc 1, 2); xuất Excel cả môn; bấm dòng → W16.
 */
export function TeamsPage() {
  const [course, setCourse] = useCourseParam()
  const semester = useCurrentSemester()
  const domains = useDomains()
  const navigate = useNavigate()
  const [exportError, setExportError] = useState<string | null>(null)
  const [exporting, setExporting] = useState(false)

  const download = async () => {
    setExporting(true)
    setExportError(null)
    try {
      await exportTeams(semester.data?.id, course)
    } catch (error) {
      setExportError(toApiError(error).detail ?? toApiError(error).title)
    } finally {
      setExporting(false)
    }
  }

  return (
    <>
      <PageHeader
        title="Nhóm"
        action={
          <Button icon="download" loading={exporting} onClick={() => void download()}>
            Xuất Excel
          </Button>
        }
      />
      {exportError && (
        <div className="mb-4">
          <Banner tone="error" title="Không xuất được file Excel">{exportError}</Banner>
        </div>
      )}
      <div className="mb-4">
        <CourseToggle value={course} onChange={setCourse} />
      </div>
      <DataTable
        queryKey={adminTeamKeys.list(course)}
        fetchPage={fetchAdminTeams(course)}
        columns={columns}
        getRowId={(t) => t.id}
        searchPlaceholder="Tên nhóm hoặc đề tài"
        filters={[
          { key: 'status', label: 'Trạng thái', options: statusOptions },
          { key: 'domain', label: 'Domain', options: (domains.data ?? []).map((d) => ({ value: d.code, label: `${d.code} · ${d.name}` })) },
          { key: 'invalidOnly', label: 'Điều kiện', options: [{ value: 'true', label: 'Chưa đủ điều kiện' }] },
        ]}
        onRowClick={(t) => navigate(`/admin/nhom/${t.id}?mon=${course}`)}
        empty={<EmptyState icon="groups" title="Chưa có nhóm" description={`Học kỳ này chưa có nhóm ${course} nào.`} />}
      />
    </>
  )
}
