import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { toApiError } from '@/shared/api/apiClient'
import { EmptyState, ErrorState } from '@/shared/components/States'
import { Banner, Button, Skeleton } from '@/shared/components/ui'
import { PageHeader } from '@/shared/layout/PageHeader'
import type { CourseCode } from '@/shared/phase/usePhase'
import { COURSES } from '@/features/dashboard/adminApi'
import { downloadTemplate, usePreviewImport, useSemesters, type ImportBatch, type ImportPreview } from './api'
import { BaselineSection } from './BaselineSection'
import { FileDrop } from './FileDrop'
import { PreviewStep } from './PreviewStep'

const selectClass = 'h-10 w-full rounded-sm border border-border-strong bg-surface px-3 text-body'

/** Màn tổng kết sau khi xác nhận. */
function ResultStep({ batch, onAgain }: { batch: ImportBatch; onAgain: () => void }) {
  const failed = batch.status === 'FAILED'
  return (
    <div className="flex flex-col gap-4">
      <Banner tone={failed ? 'error' : 'success'} title={failed ? 'Không import được dòng nào' : `Đã import ${batch.successRows} sinh viên`}>
        <p>
          {batch.fileName}: {batch.totalRows} dòng, {batch.successRows} thành công, {batch.failedRows} bị bỏ qua.
        </p>
      </Banner>
      <div className="flex justify-end gap-2">
        <Link
          to="/admin/sinh-vien"
          className="inline-flex min-h-10 items-center rounded-md border border-primary bg-surface px-4 text-body font-medium text-primary-dark hover:bg-primary-container"
        >
          Xem danh sách sinh viên
        </Link>
        <Button icon="upload_file" onClick={onAgain}>
          Import file khác
        </Button>
      </div>
    </div>
  )
}

/** W06 – chọn kỳ, môn, tải file; EXE201 có thêm file nhóm EXE101 làm mốc. */
function UploadStep({ onPreview }: { onPreview: (preview: ImportPreview) => void }) {
  const semesters = useSemesters()
  const [semesterId, setSemesterId] = useState('')
  const [course, setCourse] = useState<CourseCode>('EXE101')
  const [file, setFile] = useState<File | null>(null)
  const [templateError, setTemplateError] = useState<unknown>(null)
  const [downloading, setDownloading] = useState(false)
  const preview = usePreviewImport()
  const error = preview.error ? toApiError(preview.error) : null
  const fileError = error?.fieldErrors.file?.[0]

  useEffect(() => {
    if (!semesterId && semesters.data?.length) {
      setSemesterId((semesters.data.find((s) => s.isCurrent) ?? semesters.data[0]).id)
    }
  }, [semesterId, semesters.data])

  const template = async () => {
    setDownloading(true)
    setTemplateError(null)
    try {
      await downloadTemplate()
    } catch (e) {
      setTemplateError(e)
    } finally {
      setDownloading(false)
    }
  }

  if (semesters.isLoading) {
    return (
      <div role="status" aria-label="Đang tải" className="flex flex-col gap-3">
        <Skeleton className="h-10 w-full" />
        <Skeleton className="h-40 w-full" />
      </div>
    )
  }
  if (semesters.isError) return <ErrorState error={semesters.error} onRetry={() => semesters.refetch()} />
  if (!semesters.data?.length) {
    return (
      <EmptyState
        icon="calendar_month"
        title="Chưa có học kỳ"
        description="Tạo học kỳ trước khi import sinh viên."
        action={
          <Link to="/admin/hoc-ky" className="inline-flex min-h-10 items-center rounded-md bg-primary px-4 font-medium text-white">
            Tạo học kỳ
          </Link>
        }
      />
    )
  }

  return (
    <div className="flex flex-col gap-4">
      <section className="flex flex-col gap-4 rounded-lg bg-surface p-6 shadow-elevation-1">
        <div className="grid gap-4 sm:grid-cols-2">
          <label className="flex flex-col gap-1 text-body font-medium">
            Học kỳ
            <select value={semesterId} onChange={(e) => setSemesterId(e.target.value)} className={selectClass}>
              {semesters.data.map((s) => (
                <option key={s.id} value={s.id}>
                  {s.code} – {s.name}
                </option>
              ))}
            </select>
          </label>
          <label className="flex flex-col gap-1 text-body font-medium">
            Môn
            <select value={course} onChange={(e) => setCourse(e.target.value as CourseCode)} className={selectClass}>
              {COURSES.map((c) => (
                <option key={c} value={c}>
                  {c}
                </option>
              ))}
            </select>
          </label>
        </div>

        <FileDrop
          label="Chọn file danh sách sinh viên"
          file={file}
          error={fileError}
          onChange={(next) => {
            setFile(next)
            preview.reset()
          }}
        />

        {error && !fileError && <ErrorState error={error} />}
        {templateError != null && <ErrorState error={templateError} />}

        <div className="flex flex-wrap items-center justify-between gap-2">
          <Button variant="text" icon="download" loading={downloading} onClick={template}>
            Tải file mẫu
          </Button>
          <Button
            icon="upload"
            disabled={!file || !semesterId}
            loading={preview.isPending}
            onClick={() => file && preview.mutate({ file, semesterId, courseCode: course }, { onSuccess: onPreview })}
          >
            Tải lên và xem trước
          </Button>
        </div>
      </section>

      {course === 'EXE201' && semesterId && <BaselineSection key={semesterId} semesterId={semesterId} />}
    </div>
  )
}

/** W06 → W07 → tổng kết (FE-12, FE-13). Bản xem trước chỉ ở state: server giữ 30 phút và không có API đọc lại. */
export function ImportPage() {
  const [preview, setPreview] = useState<ImportPreview | null>(null)
  const [batch, setBatch] = useState<ImportBatch | null>(null)

  const restart = () => {
    setPreview(null)
    setBatch(null)
  }

  return (
    <div className="mx-auto flex w-full min-w-0 max-w-[1120px] flex-col gap-4">
      <PageHeader title={batch ? 'Kết quả import' : preview ? 'Xem trước import' : 'Import sinh viên'} />
      {batch ? (
        <ResultStep batch={batch} onAgain={restart} />
      ) : preview ? (
        <PreviewStep preview={preview} onBack={restart} onDone={setBatch} />
      ) : (
        <UploadStep onPreview={setPreview} />
      )}
    </div>
  )
}
