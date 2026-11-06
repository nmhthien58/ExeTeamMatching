import { useState } from 'react'
import type { ColumnDef } from '@tanstack/react-table'
import { DataTable } from '@/shared/components/DataTable'
import { EmptyState } from '@/shared/components/States'
import { Button, StatusChip } from '@/shared/components/ui'
import { PageHeader } from '@/shared/layout/PageHeader'
import { useSnackbar } from '@/features/workspace/useSnackbar'
import { useDomainCatalog } from '@/features/lecturers/api'
import { ActiveChip, DomainCodes } from '@/features/lecturers/form'
import { fetchMentors, mentorKeys, type Mentor } from './api'
import { MentorDrawer } from './MentorDrawer'
import { MentorSettings } from './MentorSettings'

const columns: ColumnDef<Mentor, unknown>[] = [
  {
    header: 'Họ tên',
    id: 'name',
    cell: ({ row }) => (
      <span className="flex flex-col">
        <span className="font-medium">{row.original.fullName}</span>
        <span className="text-caption text-text-secondary">{row.original.email}</span>
      </span>
    ),
  },
  {
    header: 'Tổ chức',
    id: 'organization',
    meta: { minWidth: 140 },
    cell: ({ row }) => (
      <span className="flex flex-col items-start gap-1">
        <span>{row.original.organization ?? '–'}</span>
        <StatusChip tone={row.original.isInternal ? 'info' : 'neutral'} icon={row.original.isInternal ? 'school' : 'business'}>
          {row.original.lecturerId ? 'Giảng viên' : row.original.isInternal ? 'Trong trường' : 'Ngoài trường'}
        </StatusChip>
      </span>
    ),
  },
  { header: 'Chuyên môn', id: 'domains', cell: ({ row }) => <DomainCodes domains={row.original.domains} /> },
  {
    header: 'Đang / mong muốn / tối đa',
    id: 'load',
    meta: { align: 'right', minWidth: 120 },
    cell: ({ row }) => {
      const m = row.original
      return (
        <span className={m.currentTeamCount >= m.maxTeams ? 'font-semibold text-warning' : undefined}>
          {m.currentTeamCount} / {m.preferredTeams ?? '–'} / {m.maxTeams}
        </span>
      )
    },
  },
  { header: 'Trạng thái', id: 'status', meta: { minWidth: 160 }, cell: ({ row }) => <ActiveChip active={row.original.isActive} /> },
]

/**
 * W27 – Mentor (FE-34, rule 20): chỉ Bộ môn tạo mentor; số nhóm tối đa đặt riêng từng mentor, mentor tự chọn số mong muốn;
 * tạo, sửa, khóa trong drawer; cài đặt tuần bắt đầu mentor và số buổi dự kiến của học kỳ.
 */
export function MentorsPage() {
  const [editing, setEditing] = useState<Mentor | null | undefined>(undefined)
  const domains = useDomainCatalog()
  const snackbar = useSnackbar()

  return (
    <>
      <PageHeader title="Mentor" action={<Button icon="person_add" onClick={() => setEditing(null)}>Thêm mentor</Button>} />
      <div className="mb-4">
        <MentorSettings onSaved={snackbar.show} />
      </div>
      <DataTable
        queryKey={mentorKeys.all}
        fetchPage={fetchMentors}
        columns={columns}
        getRowId={(m) => m.id}
        searchPlaceholder="Họ tên, email hoặc tổ chức"
        filters={[
          { key: 'isInternal', label: 'Loại', options: [{ value: 'true', label: 'Trong trường' }, { value: 'false', label: 'Ngoài trường' }] },
          { key: 'hasCapacity', label: 'Còn nhận nhóm', options: [{ value: 'true', label: 'Còn nhận' }, { value: 'false', label: 'Đã đủ' }] },
          { key: 'domainId', label: 'Domain', options: (domains.data ?? []).map((d) => ({ value: String(d.id), label: `${d.code} · ${d.name}` })) },
        ]}
        onRowClick={setEditing}
        empty={
          <EmptyState
            icon="support_agent"
            title="Chưa có mentor"
            description="Thêm mentor để hệ thống ghép cho các nhóm từ tuần bắt đầu mentor."
            action={<Button icon="person_add" onClick={() => setEditing(null)}>Thêm mentor</Button>}
          />
        }
      />
      {editing !== undefined && (
        <MentorDrawer
          key={editing?.id ?? 'new'}
          mentor={editing}
          onClose={() => setEditing(undefined)}
          onSaved={(message) => {
            setEditing(undefined)
            snackbar.show(message)
          }}
        />
      )}
      {snackbar.element}
    </>
  )
}
