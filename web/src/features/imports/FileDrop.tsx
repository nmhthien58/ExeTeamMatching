import { useId, useState, type DragEvent } from 'react'
import clsx from 'clsx'
import { Button, Icon } from '@/shared/components/ui'
import { MAX_FILE_MB, fileProblem } from './api'

function formatSize(bytes: number) {
  return bytes < 1024 * 1024 ? `${Math.max(1, Math.round(bytes / 1024))} KB` : `${(bytes / 1024 / 1024).toFixed(1)} MB`
}

/** Ô kéo thả file Excel. Trạng thái: chưa chọn file, đã chọn, sai định dạng; lỗi từ server truyền qua `error`. */
export function FileDrop({
  label,
  file,
  error,
  onChange,
}: {
  label: string
  file: File | null
  error?: string
  onChange: (file: File | null) => void
}) {
  const id = useId()
  const [dragging, setDragging] = useState(false)
  const [problem, setProblem] = useState<string | null>(null)
  const message = problem ?? error

  const choose = (next: File | undefined) => {
    if (!next) return
    const invalid = fileProblem(next)
    setProblem(invalid)
    onChange(invalid ? null : next)
  }
  const onDrop = (e: DragEvent) => {
    e.preventDefault()
    setDragging(false)
    choose(e.dataTransfer.files[0])
  }

  return (
    <div>
      {file ? (
        <div className="flex items-center gap-3 rounded-md border border-border-strong bg-surface px-4 py-3">
          <Icon name="table_view" className="text-text-secondary" />
          <div className="min-w-0 flex-1">
            <p className="truncate text-body font-medium">{file.name}</p>
            <p className="text-caption text-text-secondary">{formatSize(file.size)}</p>
          </div>
          <Button variant="text" icon="close" onClick={() => onChange(null)}>
            Bỏ file
          </Button>
        </div>
      ) : (
        <label
          htmlFor={id}
          onDragOver={(e) => {
            e.preventDefault()
            setDragging(true)
          }}
          onDragLeave={() => setDragging(false)}
          onDrop={onDrop}
          className={clsx(
            'flex cursor-pointer flex-col items-center gap-1 rounded-md border border-dashed px-6 py-8 text-center focus-within:outline-2 focus-within:outline-primary',
            dragging ? 'border-primary bg-primary-container' : 'border-border-strong bg-surface',
          )}
        >
          <Icon name="upload_file" size={48} className="text-text-secondary" />
          <span className="text-subtitle font-semibold">{label}</span>
          <span className="text-body text-text-secondary">Kéo thả vào đây hoặc bấm để chọn. File .xlsx, tối đa {MAX_FILE_MB} MB.</span>
          <input
            id={id}
            type="file"
            accept=".xlsx"
            className="sr-only"
            onChange={(e) => {
              choose(e.target.files?.[0])
              e.target.value = ''
            }}
          />
        </label>
      )}
      {message && (
        <p role="alert" className="mt-2 text-body text-error">
          {message}
        </p>
      )}
    </div>
  )
}
