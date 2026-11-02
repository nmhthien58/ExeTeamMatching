import { EmptyState, ErrorState, TableSkeleton } from '@/shared/components/States'
import { Button, StatusChip } from '@/shared/components/ui'
import { COURSES } from '@/features/dashboard/adminApi'
import { useSemesterList, type Semester } from './api'

export function formatDate(value: string) {
  return new Intl.DateTimeFormat('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric', timeZone: 'UTC' }).format(new Date(`${value}T00:00:00Z`))
}

const th = 'h-11 px-4 text-left text-caption font-semibold text-text-secondary'

/** W03 – danh sách học kỳ và hai môn EXE101, EXE201 của mỗi kỳ. */
export function SemestersTab({ onCreate, onEdit }: { onCreate: () => void; onEdit: (semester: Semester) => void }) {
  const semesters = useSemesterList()

  if (semesters.isError) return <ErrorState error={semesters.error} onRetry={() => semesters.refetch()} />
  return (
    <div className="overflow-x-auto rounded-lg bg-surface shadow-elevation-1">
      {semesters.isLoading ? (
        <TableSkeleton columns={5} />
      ) : !semesters.data?.length ? (
        <EmptyState
          icon="calendar_month"
          title="Chưa có học kỳ"
          description="Tạo học kỳ đầu tiên để import sinh viên và mở đợt đăng ký."
          action={<Button onClick={onCreate}>Tạo học kỳ</Button>}
        />
      ) : (
        <table className="w-full border-collapse text-body">
          <thead className="bg-background">
            <tr>
              <th scope="col" className={th}>Mã</th>
              <th scope="col" className={th}>Tên</th>
              <th scope="col" className={th}>Thời gian</th>
              <th scope="col" className={th}>Môn</th>
              <th scope="col" className={th}>Trạng thái</th>
              <th scope="col" className={th}><span className="sr-only">Thao tác</span></th>
            </tr>
          </thead>
          <tbody>
            {semesters.data.map((s) => (
              <tr key={s.id} className="border-t border-border">
                <td className="h-[52px] px-4 font-medium">{s.code}</td>
                <td className="px-4">{s.name}</td>
                <td className="whitespace-nowrap px-4">
                  {formatDate(s.startDate)} – {formatDate(s.endDate)}
                </td>
                <td className="whitespace-nowrap px-4">{COURSES.join(', ')}</td>
                <td className="px-4">{s.isCurrent && <StatusChip tone="success">Hiện hành</StatusChip>}</td>
                <td className="px-4 text-right">
                  <Button variant="text" icon="edit" aria-label={`Sửa ${s.code}`} onClick={() => onEdit(s)}>
                    Sửa
                  </Button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  )
}
