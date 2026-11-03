import { useState } from 'react'
import { EmptyState, ErrorState, TableSkeleton } from '@/shared/components/States'
import { Button, StatusChip, type StatusTone } from '@/shared/components/ui'
import { useImportHistory, useSemesters, type ImportBatch } from './api'

const statusChip: Record<ImportBatch['status'], { tone: StatusTone; label: string }> = {
  COMPLETED: { tone: 'success', label: 'Hoàn tất' },
  PROCESSING: { tone: 'info', label: 'Đang xử lý' },
  FAILED: { tone: 'error', label: 'Thất bại' },
}

const formatTime = (value: string) =>
  new Intl.DateTimeFormat('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' }).format(new Date(value))

const th = 'h-11 px-4 text-left text-caption font-semibold text-text-secondary'
const thRight = `${th} text-right`

/**
 * W08 – Lịch sử import (FE-14): người thực hiện, thời điểm, số dòng thành công / lỗi; lọc theo học kỳ.
 * Bảng riêng thay vì DataTable vì API không có tìm kiếm.
 */
export function ImportHistory() {
  const semesters = useSemesters()
  const [semesterId, setSemesterId] = useState('')
  const [page, setPage] = useState(1)
  const history = useImportHistory(semesterId || undefined, page)
  const totalPages = history.data?.totalPages ?? 1

  return (
    <section aria-labelledby="import-history" className="flex flex-col gap-3">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h2 id="import-history" className="text-subtitle font-semibold">Lịch sử import</h2>
        <label className="flex items-center gap-2 text-body">
          Lọc theo kỳ
          <select
            value={semesterId}
            onChange={(e) => {
              setSemesterId(e.target.value)
              setPage(1)
            }}
            className="h-10 rounded-sm border border-border-strong bg-surface px-3"
          >
            <option value="">Tất cả</option>
            {(semesters.data ?? []).map((s) => (
              <option key={s.id} value={s.id}>{s.code}</option>
            ))}
          </select>
        </label>
      </div>
      {history.isError ? (
        <ErrorState error={history.error} onRetry={() => void history.refetch()} />
      ) : (
        <div className="overflow-x-auto rounded-lg bg-surface shadow-elevation-1">
          {history.isLoading ? (
            <TableSkeleton columns={7} rows={4} />
          ) : !history.data?.items.length ? (
            <EmptyState icon="history" title="Chưa có lần import nào" description="Các lần import danh sách sinh viên sẽ hiện ở đây." />
          ) : (
            <table className="w-full border-collapse text-body">
              <thead className="bg-background">
                <tr>
                  <th scope="col" className={th}>Thời điểm</th>
                  <th scope="col" className={th}>File</th>
                  <th scope="col" className={th}>Người thực hiện</th>
                  <th scope="col" className={thRight}>Tổng dòng</th>
                  <th scope="col" className={thRight}>Thành công</th>
                  <th scope="col" className={thRight}>Lỗi</th>
                  <th scope="col" className={th}>Trạng thái</th>
                </tr>
              </thead>
              <tbody>
                {history.data.items.map((b) => (
                  <tr key={b.id} className="border-t border-border">
                    <td className="h-[52px] whitespace-nowrap px-4">{formatTime(b.createdAt)}</td>
                    <td className="break-all px-4">{b.fileName}</td>
                    <td className="px-4">{b.importedByName ?? '–'}</td>
                    <td className="px-4 text-right">{b.totalRows}</td>
                    <td className="px-4 text-right">{b.successRows}</td>
                    <td className={b.failedRows > 0 ? 'px-4 text-right font-semibold text-error' : 'px-4 text-right'}>{b.failedRows}</td>
                    <td className="px-4">
                      <StatusChip tone={statusChip[b.status].tone}>{statusChip[b.status].label}</StatusChip>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      )}
      {totalPages > 1 && (
        <div className="flex items-center justify-end gap-2 text-body">
          <span className="text-text-secondary">Trang {page}/{totalPages}</span>
          <Button variant="text" icon="chevron_left" aria-label="Trang trước" disabled={page <= 1} onClick={() => setPage((p) => p - 1)} />
          <Button variant="text" icon="chevron_right" aria-label="Trang sau" disabled={page >= totalPages} onClick={() => setPage((p) => p + 1)} />
        </div>
      )}
    </section>
  )
}
