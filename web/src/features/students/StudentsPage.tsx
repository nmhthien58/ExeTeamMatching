import { useState } from 'react'
import type { ColumnDef } from '@tanstack/react-table'
import { Link } from 'react-router-dom'
import { DataTable } from '@/shared/components/DataTable'
import { EmptyState } from '@/shared/components/States'
import { Button, StatusChip } from '@/shared/components/ui'
import { PageHeader } from '@/shared/layout/PageHeader'
import { fetchStudents, studentKeys, useStudentFilterCatalogs, type StudentSummary } from './api'
import { StudentDrawer } from './StudentDrawer'

const columns: ColumnDef<StudentSummary, unknown>[] = [
  {
    header: 'Sinh viên',
    id: 'name',
    cell: ({ row }) => (
      <span className="flex flex-col">
        <span className="font-medium">{row.original.fullName}</span>
        <span className="text-caption text-text-secondary">{row.original.studentCode} · {row.original.email}</span>
      </span>
    ),
  },
  { header: 'Chuyên ngành', id: 'specialization', cell: ({ row }) => row.original.specialization?.name ?? '–' },
  { header: 'Mã lớp', id: 'classCode', cell: ({ row }) => row.original.classCode ?? '–' },
  { header: 'Môn', id: 'courseCode', cell: ({ row }) => row.original.courseCode ?? '–' },
  {
    header: 'Nhóm',
    id: 'team',
    meta: { minWidth: 150 },
    cell: ({ row }) =>
      row.original.hasTeam ? (
        <StatusChip tone="success" icon="groups">Đã có nhóm</StatusChip>
      ) : (
        <StatusChip tone={row.original.isSeekingTeam ? 'warning' : 'neutral'} icon="person_search">
          {row.original.isSeekingTeam ? 'Đang tìm nhóm' : 'Chưa có nhóm'}
        </StatusChip>
      ),
  },
]

/** W09 – Danh sách sinh viên (FE-15): học kỳ hiện hành, tìm tên / MSSV, lọc môn, mã lớp, chuyên ngành, đã có nhóm; 20 dòng / trang. */
export function StudentsPage() {
  const [openId, setOpenId] = useState<string | null>(null)
  const { classCodes, specializations } = useStudentFilterCatalogs()

  return (
    <>
      <PageHeader title="Sinh viên" />
      <DataTable
        queryKey={studentKeys.all}
        fetchPage={fetchStudents}
        columns={columns}
        getRowId={(s) => s.id}
        searchPlaceholder="Họ tên hoặc MSSV"
        filters={[
          { key: 'courseCode', label: 'Môn', options: [{ value: 'EXE101', label: 'EXE101' }, { value: 'EXE201', label: 'EXE201' }] },
          { key: 'classCode', label: 'Mã lớp', options: classCodes.map((c) => ({ value: c.code, label: c.code })) },
          { key: 'specializationId', label: 'Chuyên ngành', options: specializations.map((s) => ({ value: String(s.id), label: s.name })) },
          { key: 'hasTeam', label: 'Nhóm', options: [{ value: 'true', label: 'Đã có nhóm' }, { value: 'false', label: 'Chưa có nhóm' }] },
        ]}
        onRowClick={(s) => setOpenId(s.id)}
        empty={
          <EmptyState
            icon="school"
            title="Chưa có sinh viên"
            description="Import danh sách sinh viên của học kỳ để bắt đầu."
            action={
              <Link to="/admin/import">
                <Button icon="upload_file" tabIndex={-1}>Import sinh viên</Button>
              </Link>
            }
          />
        }
      />
      {openId && <StudentDrawer key={openId} id={openId} onClose={() => setOpenId(null)} />}
    </>
  )
}
