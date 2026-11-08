import { useMemo, useState } from 'react'
import type { ColumnDef } from '@tanstack/react-table'
import { DataTable } from '@/shared/components/DataTable'
import { EmptyState } from '@/shared/components/States'
import { Button } from '@/shared/components/ui'
import { PageHeader } from '@/shared/layout/PageHeader'
import { useSnackbar } from '@/features/workspace/useSnackbar'
import { fetchLecturers, lecturerKeys, useLecturerLoads, type Lecturer } from './api'
import { ActiveChip, DomainCodes } from './form'
import { LecturerDrawer } from './LecturerDrawer'
import { LecturerImportDialog } from './LecturerImportDialog'

/** "đang / tối đa (muốn: x)"; tối đa trống là không giới hạn. */
const load = (current: number | undefined, max: number | null, desired?: number | null) => {
  const base = `${current ?? '–'} / ${max ?? '∞'}`
  return desired != null ? `${base} (muốn: ${desired})` : base
}

/**
 * W11 – Giảng viên (FE-17, FE-57): danh sách, email cấp quyền đăng nhập, chuyên môn (rule 19),
 * số nhóm đang nhận / tối đa theo môn (rule 9), số nhóm mong muốn, import file Excel.
 * Thêm, sửa, khóa trong drawer. Số nhóm đang nhận đọc từ bảng ghép (Team Service), lỗi thì hiện "–".
 */
export function LecturersPage() {
  const [editing, setEditing] = useState<Lecturer | null | undefined>(undefined)
  const [showImport, setShowImport] = useState(false)
  const loads101 = useLecturerLoads('EXE101')
  const loads201 = useLecturerLoads('EXE201')
  const snackbar = useSnackbar()

  // Cột đổi khi số nhóm đang nhận tải xong; giữ nguyên giữa các lần render khác để ô không bị dựng lại.
  const columns = useMemo<ColumnDef<Lecturer, unknown>[]>(() => [
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
    { header: 'Chuyên môn', id: 'domains', cell: ({ row }) => <DomainCodes domains={row.original.domains} /> },
    {
      header: 'EXE101 đang / tối đa',
      id: 'exe101',
      meta: { align: 'right' },
      cell: ({ row }) => load(loads101.data?.get(row.original.id), row.original.maxTeamsExe101, row.original.desiredTeamsExe101),
    },
    {
      header: 'EXE201 đang / tối đa',
      id: 'exe201',
      meta: { align: 'right' },
      cell: ({ row }) => load(loads201.data?.get(row.original.id), row.original.maxTeamsExe201, row.original.desiredTeamsExe201),
    },
    {
      header: 'Trạng thái',
      id: 'status',
      meta: { minWidth: 160 },
      cell: ({ row }) => (
        <div className="flex flex-wrap items-center gap-1.5">
          <ActiveChip active={row.original.isActive} />
          {!row.original.isProfileCompleted && (
            <span className="inline-flex items-center rounded-full bg-status-warning/15 px-2 py-0.5 text-xs font-medium text-status-warning">
              Chưa khai báo
            </span>
          )}
        </div>
      ),
    },
  ], [loads101.data, loads201.data])

  return (
    <>
      <PageHeader
        title="Giảng viên"
        action={
          <div className="flex items-center gap-2">
            <Button variant="outline" icon="upload_file" onClick={() => setShowImport(true)}>
              Import Excel
            </Button>
            <Button icon="person_add" onClick={() => setEditing(null)}>
              Thêm giảng viên
            </Button>
          </div>
        }
      />
      <DataTable
        queryKey={lecturerKeys.all}
        fetchPage={fetchLecturers}
        columns={columns}
        getRowId={(l) => l.id}
        searchPlaceholder="Họ tên hoặc email"
        onRowClick={setEditing}
        empty={
          <EmptyState
            icon="co_present"
            title="Chưa có giảng viên"
            description="Thêm giảng viên bằng email để họ đăng nhập và nhận nhóm."
            action={
              <div className="flex items-center gap-2">
                <Button variant="outline" icon="upload_file" onClick={() => setShowImport(true)}>
                  Import Excel
                </Button>
                <Button icon="person_add" onClick={() => setEditing(null)}>
                  Thêm giảng viên
                </Button>
              </div>
            }
          />
        }
      />
      {editing !== undefined && (
        <LecturerDrawer
          key={editing?.id ?? 'new'}
          lecturer={editing}
          onClose={() => setEditing(undefined)}
          onSaved={(message) => {
            setEditing(undefined)
            snackbar.show(message)
          }}
        />
      )}
      {showImport && (
        <LecturerImportDialog
          onClose={() => setShowImport(false)}
          onSuccess={(count) => {
            snackbar.show(`Đã import thành công ${count} giảng viên`)
          }}
        />
      )}
      {snackbar.element}
    </>
  )
}
