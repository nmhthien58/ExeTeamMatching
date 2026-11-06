import { useState } from 'react'
import { Link } from 'react-router-dom'
import { ApiError, toApiError } from '@/shared/api/apiClient'
import { ErrorState } from '@/shared/components/States'
import { Banner, Button } from '@/shared/components/ui'
import { useBaselineImport, type BaselineResult } from './api'
import { FileDrop } from './FileDrop'

function Counts({ result }: { result: BaselineResult }) {
  return (
    <p>
      {result.teamCount} nhóm, {result.studentCount} sinh viên; {result.linkedProjectCount} nhóm gắn được đề tài EXE101,{' '}
      {result.unlinkedTeamCount} nhóm chưa gắn.
    </p>
  )
}

/**
 * W06 với EXE201: file nhóm EXE101 làm mốc. Bắt buộc kiểm tra trước (`dryRun`), không có dòng lỗi mới import được,
 * vì import xong là tạo nhóm kế thừa. Nhóm chưa gắn đề tài xử lý tiếp ở W29.
 */
export function BaselineSection({ semesterId }: { semesterId: string }) {
  const [file, setFile] = useState<File | null>(null)
  const [checked, setChecked] = useState<BaselineResult | null>(null)
  const [done, setDone] = useState<BaselineResult | null>(null)
  const baseline = useBaselineImport()
  const error = baseline.error ? toApiError(baseline.error) : null
  const fileError = error?.fieldErrors.file?.[0]

  const changeFile = (next: File | null) => {
    setFile(next)
    setChecked(null)
    setDone(null)
    baseline.reset()
  }
  const run = (dryRun: boolean) => {
    if (!file) return
    baseline.mutate(
      { file, semesterId, dryRun },
      {
        onSuccess: (result) => {
          setChecked(dryRun ? result : null)
          setDone(dryRun ? null : result)
        },
      },
    )
  }
  const hasErrors = (checked?.errors.length ?? 0) > 0

  return (
    <section aria-labelledby="baseline-title" className="flex flex-col gap-3 rounded-lg bg-surface p-6 shadow-elevation-1">
      <div>
        <h2 id="baseline-title" className="text-subtitle font-semibold">
          File nhóm EXE101 làm mốc
        </h2>
        <p className="text-body text-text-secondary">
          Import sau khi đã có danh sách sinh viên EXE201. Hệ thống tạo nhóm kế thừa và gắn với đề tài cũ theo mã nhóm.
        </p>
      </div>

      <FileDrop label="Chọn file nhóm EXE101" file={file} error={fileError} onChange={changeFile} />

      {error && !fileError && <ErrorState error={error instanceof ApiError ? error : baseline.error} />}

      {checked && !hasErrors && (
        <Banner tone="success" title="File hợp lệ, chưa tạo nhóm nào">
          <Counts result={checked} />
        </Banner>
      )}
      {hasErrors && (
        <Banner tone="error" title={`${checked!.errors.length} dòng lỗi, sửa file rồi kiểm tra lại`}>
          <ul className="list-disc pl-5">
            {checked!.errors.slice(0, 20).map((e, i) => (
              <li key={i}>
                Dòng {e.row}: {e.message}
              </li>
            ))}
          </ul>
          {checked!.errors.length > 20 && <p>Và {checked!.errors.length - 20} lỗi khác.</p>}
        </Banner>
      )}
      {done && (
        <Banner tone="success" title="Đã import mốc EXE101">
          <Counts result={done} />
          {done.unlinkedTeamCount > 0 && (
            <Link to="/admin/de-tai" className="font-medium text-primary-dark underline">
              Gắn đề tài cho nhóm chưa khớp
            </Link>
          )}
        </Banner>
      )}

      {!done && (
        <div className="flex justify-end gap-2">
          <Button variant="outline" disabled={!file} loading={baseline.isPending && baseline.variables?.dryRun} onClick={() => run(true)}>
            Kiểm tra file
          </Button>
          <Button variant="outline" disabled={!checked || hasErrors} loading={baseline.isPending && !baseline.variables?.dryRun} onClick={() => run(false)}>
            Import mốc
          </Button>
        </div>
      )}
    </section>
  )
}
