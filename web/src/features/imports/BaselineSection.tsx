import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { ApiError, toApiError } from '@/shared/api/apiClient'
import { ErrorState } from '@/shared/components/States'
import { Banner, Button, StatusChip } from '@/shared/components/ui'
import { useDomains } from '@/features/my_team/api'
import {
  useAutoInherit,
  useBaselineImport,
  type AutoInheritResponse,
  type BaselineResult,
  type InheritedTeamSummary,
} from './api'
import { FileDrop } from './FileDrop'

function Counts({ result }: { result: BaselineResult }) {
  return (
    <p>
      {result.teamCount} nhóm kế thừa, {result.studentCount} sinh viên
      {(result.needsReviewTeamCount ?? 0) > 0 && `; ${result.needsReviewTeamCount} nhóm cần bổ sung thành viên`}
      {(result.skippedTeamCount ?? 0) > 0 && `; ${result.skippedTeamCount} nhóm cũ không còn ai học EXE201 kỳ này`}
      {(result.freeStudentCount ?? 0) > 0 && `; còn ${result.freeStudentCount} sinh viên tự do`}. {result.linkedProjectCount} nhóm gắn
      được đề tài EXE101, {result.unlinkedTeamCount} nhóm chưa gắn.
    </p>
  )
}

/** Nhóm kế thừa sau khi đối chiếu: đủ sĩ số tối thiểu là ACTIVE, thiếu là DRAFT (còn nhận thêm người). */
function InheritedTeamsTable({ teams }: { teams: InheritedTeamSummary[] }) {
  const domainsQuery = useDomains()
  const domainMap = new Map((domainsQuery.data ?? []).map((d) => [d.code, d.name]))

  return (
    <div className="max-h-80 overflow-y-auto rounded border border-border">
      <table className="w-full text-left text-body">
        <thead className="bg-surface-variant text-caption uppercase text-text-secondary sticky top-0">
          <tr>
            <th className="px-3 py-2">Tên nhóm</th>
            <th className="px-3 py-2">Trưởng nhóm</th>
            <th className="px-3 py-2">Thành viên</th>
            <th className="px-3 py-2">Đề tài cũ</th>
            <th className="px-3 py-2">Domain</th>
            <th className="px-3 py-2">Trạng thái</th>
          </tr>
        </thead>
        <tbody className="divide-y divide-border">
          {teams.map((t, idx) => {
            const domainLabels = (t.domainCodes ?? []).map((code) => {
              const name = domainMap.get(code)
              return name ? `${code} · ${name}` : code
            })

            return (
              <tr key={idx} className="hover:bg-surface-variant/50">
                <td className="px-3 py-2 font-medium">{t.teamName}</td>
                <td className="px-3 py-2">
                  {t.leaderName} ({t.leaderStudentCode})
                </td>
                <td className="px-3 py-2">
                  {t.memberCount} thành viên
                  {t.missingOldMemberStudentCodes.length > 0 && (
                    <span className="block text-caption text-text-secondary">
                      Không học EXE201 kỳ này: {t.missingOldMemberStudentCodes.join(', ')}
                    </span>
                  )}
                </td>
                <td className="px-3 py-2">
                  {t.projectName ? (
                    <div>
                      <span className="font-medium text-text-primary">{t.projectName}</span>
                      <span className="block text-caption text-text-secondary">
                        {t.oldTeamId ? 'Gắn đề tài EXE101 có sẵn' : 'Tạo mới từ file'}
                      </span>
                    </div>
                  ) : (
                    <span className="text-text-secondary">—</span>
                  )}
                </td>
                <td className="px-3 py-2 text-text-secondary">
                  {domainLabels.length > 0 ? (
                    <div className="flex flex-wrap gap-1">
                      {domainLabels.map((label, dIdx) => (
                        <span key={dIdx} className="inline-block rounded bg-surface-variant px-1.5 py-0.5 text-caption font-medium text-text-primary">
                          {label}
                        </span>
                      ))}
                    </div>
                  ) : (
                    '—'
                  )}
                </td>
                <td className="px-3 py-2">
                  <StatusChip tone={t.status === 'ACTIVE' ? 'success' : 'warning'}>
                    {t.status === 'ACTIVE' ? 'Đủ người' : 'Cần bổ sung'}
                  </StatusChip>
                </td>
              </tr>
            )
          })}
        </tbody>
      </table>
    </div>
  )
}

/**
 * W06 với EXE201: Tự động đối chiếu kế thừa nhóm EXE101 kỳ trước (BE-92, FE-56)
 * và phương án dự phòng: upload file nhóm EXE101 làm mốc thủ công.
 */
export function BaselineSection({ semesterId }: { semesterId: string }) {
  const navigate = useNavigate()
  // Manual baseline import state
  const [file, setFile] = useState<File | null>(null)
  const [checked, setChecked] = useState<BaselineResult | null>(null)
  const [done, setDone] = useState<BaselineResult | null>(null)
  const baseline = useBaselineImport()
  const error = baseline.error ? toApiError(baseline.error) : null
  const fileError = error?.fieldErrors.file?.[0]

  // Auto-inherit state
  const [inheritPreview, setInheritPreview] = useState<AutoInheritResponse | null>(null)
  const [inheritDone, setInheritDone] = useState<AutoInheritResponse | null>(null)
  const autoInherit = useAutoInherit()
  const inheritError = autoInherit.error ? toApiError(autoInherit.error) : null

  const handleAutoInheritPreview = () => {
    autoInherit.mutate(
      { semesterId, dryRun: true },
      {
        onSuccess: (data) => {
          setInheritPreview(data)
          setInheritDone(null)
        },
      },
    )
  }

  const handleAutoInheritConfirm = () => {
    autoInherit.mutate(
      { semesterId, dryRun: false },
      {
        onSuccess: (data) => {
          setInheritDone(data)
          setInheritPreview(null)
        },
      },
    )
  }

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
    <section aria-labelledby="baseline-title" className="flex flex-col gap-6 rounded-lg bg-surface p-6 shadow-elevation-1">
      <div>
        <h2 id="baseline-title" className="text-subtitle font-semibold">
          Kế thừa nhóm EXE101 cho EXE201
        </h2>
        <p className="text-body text-text-secondary">
          Hệ thống sẽ tự động kế thừa nhóm từ EXE101 kỳ trước. Bạn cũng có thể tải file mốc nếu muốn dùng danh sách khác.
        </p>
      </div>

      {/* Auto-inherit Primary Option */}
      <div className="flex flex-col gap-3 rounded-md border border-border-strong p-4 bg-surface-variant/30">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div>
            <h3 className="text-body font-semibold text-text-primary">Tự động kế thừa từ EXE101 kỳ trước</h3>
            <p className="text-caption text-text-secondary">
              Đối chiếu danh sách sinh viên EXE201 hiện tại với các nhóm EXE101 đã hoàn thành để tự động tạo nhóm.
            </p>
          </div>
          <Button
            variant="primary"
            icon="sync"
            loading={autoInherit.isPending && autoInherit.variables?.dryRun}
            onClick={handleAutoInheritPreview}
          >
            Tự động kế thừa từ EXE101
          </Button>
        </div>

        {inheritError && <ErrorState error={inheritError} />}

        {inheritDone && (
          <Banner tone="success" title="Đã tạo nhóm kế thừa thành công!">
            <p>
              Đã tạo {inheritDone.inheritedTeamCount} nhóm kế thừa ({inheritDone.inheritedStudentCount} sinh viên).
              {inheritDone.needsReviewTeamCount > 0 && ` Có ${inheritDone.needsReviewTeamCount} nhóm cần bổ sung thành viên.`}
              {inheritDone.freeStudentCount > 0 && ` Còn ${inheritDone.freeStudentCount} sinh viên tự do.`}
            </p>
            <div className="mt-3 flex gap-2">
              <Button variant="outline" onClick={() => navigate('/admin/nhom')}>
                Chuyển tới Quản lý nhóm (W15)
              </Button>
            </div>
          </Banner>
        )}

        {inheritPreview && !inheritDone && (
          <div className="mt-3 flex flex-col gap-4">
            <Banner tone="info" title="Xem trước kết quả kế thừa nhóm">
              <div className="grid gap-2 sm:grid-cols-3 mt-1">
                <div className="rounded bg-surface p-2 shadow-sm">
                  <span className="text-caption text-text-secondary">Nhóm đủ người:</span>
                  <p className="text-subtitle font-bold text-success">
                    {inheritPreview.inheritedTeamCount - inheritPreview.needsReviewTeamCount}
                  </p>
                </div>
                <div className="rounded bg-surface p-2 shadow-sm">
                  <span className="text-caption text-text-secondary">Cần bổ sung thành viên:</span>
                  <p className="text-subtitle font-bold text-warning">
                    {inheritPreview.needsReviewTeamCount}
                  </p>
                </div>
                <div className="rounded bg-surface p-2 shadow-sm">
                  <span className="text-caption text-text-secondary">Sinh viên tự do:</span>
                  <p className="text-subtitle font-bold text-text-primary">
                    {inheritPreview.freeStudentCount}
                  </p>
                </div>
              </div>
            </Banner>

            {inheritPreview.teams.length > 0 ? (
              <InheritedTeamsTable teams={inheritPreview.teams} />
            ) : (
              <p className="text-body text-text-secondary italic">
                Không tìm thấy nhóm EXE101 nào có sinh viên theo học kỳ này.
              </p>
            )}

            <div className="flex justify-end gap-2">
              <Button variant="outline" onClick={() => setInheritPreview(null)}>
                Hủy
              </Button>
              <Button
                variant="primary"
                disabled={inheritPreview.teams.length === 0}
                loading={autoInherit.isPending && !autoInherit.variables?.dryRun}
                onClick={handleAutoInheritConfirm}
              >
                Xác nhận tạo nhóm kế thừa
              </Button>
            </div>
          </div>
        )}
      </div>

      {/* Manual Baseline Import Secondary Option */}
      <div className="flex flex-col gap-3 border-t border-border pt-4">
        <div>
          <h3 className="text-subtitle font-semibold">
            File nhóm EXE101 làm mốc
          </h3>
          <p className="text-body text-text-secondary">
            Dùng khi hệ thống chưa có nhóm kỳ trước (kỳ đầu sử dụng): tải file Group List EXE101 của kỳ trước (.xlsx, .csv). Hệ thống đối
            chiếu với sinh viên EXE201 kỳ này, mỗi nhóm cũ giữ lại thành viên còn học, tạo nhóm kế thừa và gắn đề tài cũ nếu có.
          </p>
        </div>

        <FileDrop label="Chọn file nhóm EXE101" file={file} error={fileError} onChange={changeFile} />

        {error && !fileError && <ErrorState error={error instanceof ApiError ? error : baseline.error} />}

        {checked && !hasErrors && (
          <Banner tone={checked.teamCount > 0 ? 'success' : 'warning'} title="Kết quả đối chiếu, chưa tạo nhóm nào">
            <Counts result={checked} />
          </Banner>
        )}
        {checked && !hasErrors && (checked.teams?.length ?? 0) > 0 && <InheritedTeamsTable teams={checked.teams!} />}
        {checked && !hasErrors && (checked.warnings?.length ?? 0) > 0 && (
          <details className="text-body text-text-secondary">
            <summary className="cursor-pointer">{checked.warnings!.length} dòng bị bỏ qua</summary>
            <ul className="list-disc pl-5">
              {checked.warnings!.slice(0, 20).map((w, i) => (
                <li key={i}>
                  Dòng {w.row}: {w.message}
                </li>
              ))}
            </ul>
          </details>
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
            <Button variant="outline" disabled={!checked || hasErrors || checked.teamCount === 0} loading={baseline.isPending && !baseline.variables?.dryRun} onClick={() => run(false)}>
              Import mốc
            </Button>
          </div>
        )}
      </div>
    </section>
  )
}
