import { useState } from 'react'
import { ApiError, toApiError } from '@/shared/api/apiClient'
import { ErrorState } from '@/shared/components/States'
import { Banner, Button } from '@/shared/components/ui'
import {
  downloadLecturerTemplate,
  useConfirmLecturerImport,
  usePreviewLecturerImport,
  type LecturerImportPreview,
} from './api'

export function LecturerImportDialog({
  onClose,
  onSuccess,
}: {
  onClose: () => void
  onSuccess: (count: number) => void
}) {
  const [file, setFile] = useState<File | null>(null)
  const [preview, setPreview] = useState<LecturerImportPreview | null>(null)
  const [downloading, setDownloading] = useState(false)
  const [templateError, setTemplateError] = useState<unknown>(null)

  const previewMutation = usePreviewLecturerImport()
  const confirmMutation = useConfirmLecturerImport()

  const error = previewMutation.error
    ? toApiError(previewMutation.error)
    : confirmMutation.error
      ? toApiError(confirmMutation.error)
      : null

  const handleDownload = async () => {
    setDownloading(true)
    setTemplateError(null)
    try {
      await downloadLecturerTemplate()
    } catch (e) {
      setTemplateError(e)
    } finally {
      setDownloading(false)
    }
  }

  const handleCheckFile = () => {
    if (!file) return
    previewMutation.mutate(file, {
      onSuccess: (data) => setPreview(data),
    })
  }

  const handleConfirm = () => {
    if (!preview) return
    confirmMutation.mutate(preview.previewId, {
      onSuccess: (res) => {
        onSuccess(res.successRows)
        onClose()
      },
    })
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-ink/50 p-4 animate-fade-in">
      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby="import-dialog-title"
        className="flex max-h-[90vh] w-full max-w-2xl flex-col rounded-lg bg-surface p-6 shadow-elevation-3 overflow-hidden"
      >
        <div className="flex items-center justify-between border-b border-border pb-3">
          <h2 id="import-dialog-title" className="text-subtitle font-semibold">
            Import giảng viên từ file Excel
          </h2>
          <button
            type="button"
            className="text-text-secondary hover:text-text-primary"
            onClick={onClose}
            aria-label="Đóng"
          >
            <span className="material-symbols-rounded">close</span>
          </button>
        </div>

        <div className="flex-1 overflow-y-auto py-4 flex flex-col gap-4">
          <p className="text-body text-text-secondary">
            File Excel gồm các cột: <strong>Email</strong> (bắt buộc, đuôi @fpt.edu.vn hoặc @fe.edu.vn),{' '}
            <strong>Họ và tên</strong> (bắt buộc), <strong>SĐT</strong> (tùy chọn).
          </p>

          <div className="flex flex-wrap items-center justify-between gap-2">
            <Button variant="text" icon="download" loading={downloading} onClick={handleDownload}>
              Tải file mẫu
            </Button>
            <input
              type="file"
              id="lecturer-excel-file"
              accept=".xlsx"
              className="text-body"
              onChange={(e) => {
                const f = e.target.files?.[0] ?? null
                setFile(f)
                setPreview(null)
                previewMutation.reset()
              }}
            />
          </div>

          {templateError != null && <ErrorState error={templateError} />}
          {error && <ErrorState error={error instanceof ApiError ? error : previewMutation.error} />}

          {file && !preview && (
            <div className="flex justify-end">
              <Button
                variant="primary"
                icon="fact_check"
                loading={previewMutation.isPending}
                onClick={handleCheckFile}
              >
                Kiểm tra file
              </Button>
            </div>
          )}

          {preview && (
            <div className="flex flex-col gap-3">
              {preview.errorRows.length > 0 && (
                <Banner tone="warning" title={`${preview.errorRows.length} dòng lỗi sẽ bị bỏ qua`}>
                  <ul className="list-disc pl-5 max-h-32 overflow-y-auto text-caption">
                    {preview.errorRows.map((er, idx) => (
                      <li key={idx}>
                        Dòng {er.rowNumber} ({er.email || 'Thiếu email'}):{' '}
                        {er.errors.map((e) => e.message).join(', ')}
                      </li>
                    ))}
                  </ul>
                </Banner>
              )}

              {preview.validRows.length === 0 && (
                <Banner tone="error" title="Không có dòng nào hợp lệ để import">
                  Vui lòng kiểm tra lại file và sửa các lỗi trước khi tải lại.
                </Banner>
              )}

              {preview.validRows.length > 0 && (
                <div>
                  <h3 className="text-body font-semibold mb-2">
                    Danh sách hợp lệ ({preview.validRows.length} giảng viên)
                  </h3>
                  <div className="max-h-48 overflow-y-auto rounded border border-border">
                    <table className="w-full text-left text-body">
                      <thead className="bg-surface-variant text-caption uppercase text-text-secondary sticky top-0">
                        <tr>
                          <th className="px-3 py-1.5">Họ tên</th>
                          <th className="px-3 py-1.5">Email</th>
                          <th className="px-3 py-1.5">SĐT</th>
                        </tr>
                      </thead>
                      <tbody className="divide-y divide-border">
                        {preview.validRows.map((r, i) => (
                          <tr key={i} className="hover:bg-surface-variant/50">
                            <td className="px-3 py-1.5 font-medium">{r.fullName}</td>
                            <td className="px-3 py-1.5">{r.email}</td>
                            <td className="px-3 py-1.5 text-text-secondary">{r.phone ?? '—'}</td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                </div>
              )}
            </div>
          )}
        </div>

        <div className="flex justify-end gap-2 border-t border-border pt-3">
          <Button variant="outline" onClick={onClose}>
            Hủy
          </Button>
          <Button
            variant="primary"
            disabled={!preview || preview.validRows.length === 0}
            loading={confirmMutation.isPending}
            onClick={handleConfirm}
          >
            Xác nhận import ({preview?.validRows.length ?? 0} giảng viên)
          </Button>
        </div>
      </div>
    </div>
  )
}
