import { useState } from 'react'
import { ApiError, toApiError } from '@/shared/api/apiClient'
import { EmptyState, ErrorState } from '@/shared/components/States'
import { Banner, Button } from '@/shared/components/ui'
import { useConfirmImport, type ImportBatch, type ImportErrorRow, type ImportPreview, type ImportRow } from './api'

const PAGE_SIZE = 20

const fieldLabels: Record<string, string> = {
  studentCode: 'MSSV',
  fullName: 'Họ tên',
  email: 'Email',
  specializationCode: 'Chuyên ngành',
  classCode: 'Mã lớp',
  phone: 'SĐT',
}

const columns: { key: keyof ImportRow; label: string }[] = [
  { key: 'rowNumber', label: 'Dòng' },
  { key: 'studentCode', label: 'MSSV' },
  { key: 'fullName', label: 'Họ tên' },
  { key: 'email', label: 'Email' },
  { key: 'classCode', label: 'Mã lớp' },
  { key: 'phone', label: 'SĐT' },
]

function isErrorRow(row: ImportRow): row is ImportErrorRow {
  return 'errors' in row
}

/** Bảng dòng trong file; dữ liệu đã có sẵn ở client nên phân trang tại chỗ, 20 dòng như DataTable. */
function RowsTable({ rows }: { rows: ImportRow[] }) {
  const [page, setPage] = useState(1)
  const totalPages = Math.max(1, Math.ceil(rows.length / PAGE_SIZE))
  const shown = rows.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE)
  const showErrors = rows.some(isErrorRow)
  return (
    <div className="rounded-lg bg-surface shadow-elevation-1">
      <div className="overflow-x-auto">
        <table className="w-full border-collapse text-body">
          <thead className="bg-background">
            <tr>
              {columns.map((c) => (
                <th key={c.key} scope="col" className="h-11 px-4 text-left text-caption font-semibold text-text-secondary">
                  {c.label}
                </th>
              ))}
              {showErrors && (
                <th scope="col" className="h-11 min-w-64 px-4 text-left text-caption font-semibold text-text-secondary">
                  Lỗi
                </th>
              )}
            </tr>
          </thead>
          <tbody>
            {shown.map((row) => (
              <tr key={row.rowNumber} className="border-t border-border">
                {columns.map((c) => (
                  <td key={c.key} className="h-[52px] px-4">
                    {row[c.key] === '' || row[c.key] === null ? <span className="text-text-secondary">–</span> : row[c.key]}
                  </td>
                ))}
                {isErrorRow(row) && (
                  <td className="px-4 py-2 text-error">
                    {row.errors.map((e, i) => (
                      <p key={i}>
                        {fieldLabels[e.field] ?? e.field}: {e.message}
                      </p>
                    ))}
                  </td>
                )}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {totalPages > 1 && (
        <div className="flex items-center justify-end gap-2 border-t border-border px-4 py-2 text-body text-text-secondary">
          <span>
            {(page - 1) * PAGE_SIZE + 1}–{Math.min(page * PAGE_SIZE, rows.length)} trên {rows.length}
          </span>
          <Button variant="text" icon="chevron_left" disabled={page === 1} onClick={() => setPage(page - 1)}>
            Trước
          </Button>
          <Button variant="text" icon="chevron_right" disabled={page === totalPages} onClick={() => setPage(page + 1)}>
            Sau
          </Button>
        </div>
      )}
    </div>
  )
}

/** W07 – xem trước: dòng lỗi và dòng hợp lệ; xác nhận thì bỏ qua dòng lỗi. */
export function PreviewStep({
  preview,
  onBack,
  onDone,
}: {
  preview: ImportPreview
  onBack: () => void
  onDone: (batch: ImportBatch) => void
}) {
  const hasErrors = preview.errorRows.length > 0
  const [tab, setTab] = useState<'errors' | 'valid'>(hasErrors ? 'errors' : 'valid')
  const confirm = useConfirmImport()
  const error = confirm.error ? toApiError(confirm.error) : null
  const expired = error instanceof ApiError && error.status === 404
  const validCount = preview.validRows.length

  const tabs = [
    { value: 'errors' as const, label: `Dòng lỗi (${preview.errorRows.length})` },
    { value: 'valid' as const, label: `Dòng hợp lệ (${validCount})` },
  ]

  return (
    <div className="flex flex-col gap-4">
      <p className="text-body text-text-secondary">
        {preview.fileName}: {preview.totalRows} dòng dữ liệu. Dữ liệu chưa được lưu cho tới khi xác nhận.
      </p>

      {expired ? (
        <Banner
          tone="error"
          title="Bản xem trước đã hết hạn"
          action={
            <Button variant="outline" onClick={onBack}>
              Tải lại file
            </Button>
          }
        >
          <p>Bản xem trước giữ trong 30 phút hoặc đã được xác nhận. Tải file lên lại để xem trước.</p>
        </Banner>
      ) : (
        error && <ErrorState error={error} />
      )}

      {hasErrors && validCount > 0 && (
        <Banner tone="warning" title={`${preview.errorRows.length} dòng lỗi sẽ bị bỏ qua`}>
          <p>Chỉ {validCount} dòng hợp lệ được import. Muốn import đủ, sửa file rồi tải lên lại.</p>
        </Banner>
      )}
      {hasErrors && validCount === 0 && (
        <Banner tone="error" title="Không có dòng hợp lệ nào">
          <p>Sửa file theo các lỗi bên dưới rồi tải lên lại.</p>
        </Banner>
      )}
      {!hasErrors && <Banner tone="success" title="Tất cả dòng đều hợp lệ" />}

      <div role="tablist" aria-label="Dòng trong file" className="flex gap-2">
        {tabs.map((t) => (
          <Button key={t.value} role="tab" aria-selected={tab === t.value} variant={tab === t.value ? 'outline' : 'text'} onClick={() => setTab(t.value)}>
            {t.label}
          </Button>
        ))}
      </div>

      {tab === 'errors' ? (
        hasErrors ? <RowsTable key="errors" rows={preview.errorRows} /> : <EmptyState icon="check_circle" title="Không có dòng lỗi" />
      ) : validCount > 0 ? (
        <RowsTable key="valid" rows={preview.validRows} />
      ) : (
        <EmptyState title="Không có dòng hợp lệ" />
      )}

      <div className="flex justify-end gap-2">
        <Button variant="outline" disabled={confirm.isPending} onClick={onBack}>
          Chọn file khác
        </Button>
        <Button
          icon="check"
          disabled={validCount === 0 || expired}
          loading={confirm.isPending}
          onClick={() => confirm.mutate(preview.previewId, { onSuccess: onDone })}
        >
          Xác nhận import {validCount} sinh viên
        </Button>
      </div>
    </div>
  )
}
