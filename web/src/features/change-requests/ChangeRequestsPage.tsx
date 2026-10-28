import type { ColumnDef } from '@tanstack/react-table'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { DataTable } from '@/shared/components/DataTable'
import { EmptyState, ErrorState } from '@/shared/components/States'
import { Button, StatusChip } from '@/shared/components/ui'
import { PageHeader } from '@/shared/layout/PageHeader'
import { useCurrentSemester } from '@/features/dashboard/adminApi'
import { useChangeRequestRounds, fetchChangeRequests, type ChangeRequest, type ChangeRequestStatus, changeRequestKeys } from './api'

const tabs: { value: ChangeRequestStatus; label: string }[] = [
  { value: 'PENDING', label: 'Chờ duyệt' },
  { value: 'APPROVED', label: 'Đã duyệt' },
  { value: 'REJECTED', label: 'Đã từ chối' },
]

function formatDate(value: string) {
  return new Intl.DateTimeFormat('vi-VN', { dateStyle: 'short' }).format(new Date(value))
}

/** W17 – requests for inherited EXE201 teams; the route is registered by the shared app shell. */
export function ChangeRequestsPage() {
  const navigate = useNavigate()
  const [params, setParams] = useSearchParams()
  const selected: ChangeRequestStatus = tabs.some((tab) => tab.value === params.get('trang-thai'))
    ? (params.get('trang-thai') as ChangeRequestStatus)
    : 'PENDING'
  const semester = useCurrentSemester()
  const rounds = useChangeRequestRounds(semester.data?.id)

  const setStatus = (status: ChangeRequestStatus) => {
    const next = new URLSearchParams(params)
    next.set('trang-thai', status)
    next.delete('page')
    setParams(next, { replace: true })
  }

  const columns: ColumnDef<ChangeRequest, unknown>[] = [
    {
      header: 'Nhóm',
      id: 'team',
      cell: ({ row }) => (
        <span className="flex flex-col">
          <span className="font-medium">{row.original.team.name}</span>
          <span className="text-text-secondary">{row.original.team.memberCount} thành viên</span>
        </span>
      ),
    },
    { header: 'Đợt', accessorKey: 'roundName' },
    { header: 'Thay đổi', id: 'changedMemberCount', meta: { align: 'right' }, cell: ({ row }) => row.original.changedMemberCount },
    { header: 'Tỷ lệ', id: 'changedPercent', meta: { align: 'right' }, cell: ({ row }) => `${row.original.changedPercent}%` },
    { header: 'Ngày gửi', id: 'createdAt', cell: ({ row }) => formatDate(row.original.createdAt) },
    {
      header: 'Trạng thái',
      id: 'status',
      cell: ({ row }) => <StatusChip tone={row.original.status === 'PENDING' ? 'warning' : row.original.status === 'APPROVED' ? 'success' : 'error'}>
        {row.original.status === 'PENDING' ? 'Chờ duyệt' : row.original.status === 'APPROVED' ? 'Đã duyệt' : 'Đã từ chối'}
      </StatusChip>,
    },
    { header: '', id: 'actions', meta: { align: 'right' }, cell: ({ row }) => <Button variant="text" onClick={() => navigate(`/admin/yeu-cau-doi-nhom/${row.original.id}`)}>Xem</Button> },
  ]

  return (
    <>
      <PageHeader title="Yêu cầu đổi nhóm" />
      <div className="mb-4 flex flex-wrap items-center gap-4">
        <div role="tablist" aria-label="Trạng thái yêu cầu" className="flex gap-2 border-b border-border">
          {tabs.map((tab) => <Button key={tab.value} role="tab" aria-selected={selected === tab.value} variant={selected === tab.value ? 'outline' : 'text'} onClick={() => setStatus(tab.value)}>{tab.label}</Button>)}
        </div>
      </div>
      {rounds.isError && <ErrorState error={rounds.error} onRetry={() => void rounds.refetch()} />}
      <DataTable
        queryKey={changeRequestKeys.list(selected, params.get('roundId') ?? undefined, { page: 1, pageSize: 20, filters: {} })}
        fetchPage={(query) => fetchChangeRequests(query, selected, query.filters.roundId)}
        columns={columns}
        filters={[{ key: 'roundId', label: 'Đợt', options: (rounds.data ?? []).map((round) => ({ value: round.id, label: round.name })) }]}
        searchPlaceholder="Tên nhóm"
        empty={<EmptyState icon="inbox" title={selected === 'PENDING' ? 'Chưa có yêu cầu chờ duyệt' : 'Chưa có yêu cầu trong trạng thái này'} description="Yêu cầu đổi nhóm EXE201 sẽ xuất hiện tại đây." />}
      />
    </>
  )
}
