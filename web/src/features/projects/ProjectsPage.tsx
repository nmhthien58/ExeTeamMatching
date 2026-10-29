import { useState } from 'react'
import type { ColumnDef } from '@tanstack/react-table'
import { useSearchParams } from 'react-router-dom'
import { useAuth } from '@/shared/auth/AuthProvider'
import { DataTable } from '@/shared/components/DataTable'
import { EmptyState, ErrorState, TableSkeleton } from '@/shared/components/States'
import { Banner, Button, Dialog, Icon, StatusChip } from '@/shared/components/ui'
import { PageHeader } from '@/shared/layout/PageHeader'
import {
  fetchProjects,
  projectKeys,
  useLinkTeam,
  useSemesters,
  useUnlinkedTeams,
  type ProjectDetail,
  type ProjectVersion,
  type TeamCardResponse,
} from './api'

type TabType = 'all' | 'pivoted' | 'unlinked'

function formatDate(value: string) {
  try {
    return new Intl.DateTimeFormat('vi-VN', { dateStyle: 'short' }).format(new Date(value))
  } catch {
    return value
  }
}

export function ProjectsPage() {
  const { hasRole } = useAuth()
  const isAdmin = hasRole('Admin')
  const [params, setParams] = useSearchParams()
  const rawTab = params.get('tab') as TabType
  const activeTab: TabType = rawTab === 'unlinked' && !isAdmin ? 'all' : rawTab || 'all'
  const selectedSemesterId = params.get('semesterId') || ''

  const semestersQuery = useSemesters()
  const unlinkedTeamsQuery = useUnlinkedTeams(selectedSemesterId || undefined)
  const linkTeamMutation = useLinkTeam()

  const [selectedProjectForHistory, setSelectedProjectForHistory] = useState<ProjectDetail | null>(null)
  const [teamToLink, setTeamToLink] = useState<TeamCardResponse | null>(null)
  const [targetProjectId, setTargetProjectId] = useState<string>('')
  const [linkError, setLinkError] = useState<string | null>(null)
  const [linkSuccess, setLinkSuccess] = useState<string | null>(null)

  const setTab = (tab: TabType) => {
    const next = new URLSearchParams(params)
    next.set('tab', tab)
    next.delete('page')
    setParams(next, { replace: true })
  }

  const setSemester = (semesterId: string) => {
    const next = new URLSearchParams(params)
    if (semesterId) next.set('semesterId', semesterId)
    else next.delete('semesterId')
    next.delete('page')
    setParams(next, { replace: true })
  }

  const handleOpenLinkModal = (team: TeamCardResponse) => {
    if (!isAdmin) return
    setTeamToLink(team)
    setTargetProjectId('')
    setLinkError(null)
    setLinkSuccess(null)
  }

  const handleConfirmLink = async () => {
    if (!isAdmin || !teamToLink || !targetProjectId) return
    setLinkError(null)
    try {
      await linkTeamMutation.mutateAsync({
        projectId: targetProjectId,
        teamId: teamToLink.id,
      })
      setLinkSuccess(`Đã gắn nhóm "${teamToLink.name}" vào đề tài thành công.`)
      setTimeout(() => {
        setTeamToLink(null)
        setLinkSuccess(null)
      }, 1200)
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : 'Không thể gắn nhóm vào đề tài'
      setLinkError(message)
    }
  }

  const columns: ColumnDef<ProjectDetail, unknown>[] = [
    {
      header: 'Đề tài & Xuất xứ',
      id: 'projectInfo',
      cell: ({ row }) => {
        const p = row.original
        return (
          <div className="flex flex-col gap-1 py-1">
            <div className="flex items-center gap-2">
              <span className="font-semibold text-text-primary">{p.name}</span>
              <span className="rounded bg-primary-container px-1.5 py-0.5 text-[11px] font-medium text-primary-dark">
                v{p.currentVersion}
              </span>
            </div>
            {p.description && (
              <p className="line-clamp-2 text-caption text-text-secondary">{p.description}</p>
            )}
            <div className="flex items-center gap-2 text-caption text-text-secondary">
              <span>Gốc:</span>
              <span className="font-medium">
                {p.originSemesterCode ? `${p.originSemesterCode} (${p.originCourseCode})` : p.originCourseCode}
              </span>
            </div>
          </div>
        )
      },
    },
    {
      header: 'Chuỗi nhóm thực hiện',
      id: 'lineage',
      cell: ({ row }) => {
        const teams = row.original.teams || []
        if (teams.length === 0) {
          return <span className="text-caption text-text-secondary">Chưa có nhóm liên kết</span>
        }
        return (
          <div className="flex flex-col gap-2 py-1">
            {teams.map((t, idx) => {
              const keptPercent = t.keptMemberPercent
              return (
                <div key={t.team.id} className="flex flex-wrap items-center gap-2 text-caption">
                  <span className="rounded bg-neutral-container px-2 py-0.5 font-medium text-text-secondary">
                    {t.semesterCode || t.team.courseCode}
                  </span>
                  <span className="font-medium text-text-primary">{t.team.name}</span>
                  {keptPercent != null && (
                    <span
                      className={`rounded px-1.5 py-0.5 text-[11px] font-semibold ${
                        keptPercent >= 70
                          ? 'bg-success-container text-success'
                          : 'bg-warning-container text-warning'
                      }`}
                      title="Tỷ lệ thành viên được giữ lại từ nhóm kỳ trước"
                    >
                      Giữ {keptPercent}% TV
                    </span>
                  )}
                  {idx < teams.length - 1 && (
                    <Icon name="arrow_downward" size={20} className="text-text-disabled" />
                  )}
                </div>
              )
            })}
          </div>
        )
      },
    },
    {
      header: 'Cố vấn & Hướng dẫn',
      id: 'advisors',
      cell: ({ row }) => {
        const teams = row.original.teams || []
        if (teams.length === 0) return <span className="text-caption text-text-secondary">—</span>
        return (
          <div className="flex flex-col gap-2 py-1 text-caption">
            {teams.map((t, idx) => (
              <div
                key={t.team?.id || idx}
                className="flex flex-col gap-0.5 border-b border-border/40 pb-1.5 last:border-b-0 last:pb-0"
              >
                <span className="font-medium text-text-secondary">
                  {t.semesterCode ? `${t.semesterCode} (${t.team?.courseCode || ''})` : t.team?.courseCode}:
                </span>
                <div className="flex flex-col gap-0.5 pl-2">
                  <div>
                    <span className="text-text-secondary">GVHD: </span>
                    <span className="font-medium text-text-primary">{t.instructorName || 'Chưa phân công'}</span>
                  </div>
                  <div>
                    <span className="text-text-secondary">Mentor: </span>
                    <span className="font-medium text-text-primary">{t.mentorName || 'Chưa phân công'}</span>
                  </div>
                </div>
              </div>
            ))}
          </div>
        )
      },
    },
    {
      header: 'Thao tác',
      id: 'actions',
      meta: { align: 'right' },
      cell: ({ row }) => (
        <div className="flex justify-end gap-1">
          <Button
            variant="text"
            icon="history"
            onClick={() => setSelectedProjectForHistory(row.original)}
            aria-label={`Xem lịch sử phiên bản của đề tài ${row.original.name}`}
          >
            Lịch sử ({row.original.versions?.length || 1})
          </Button>
        </div>
      ),
    },
  ]

  const isPivoted = activeTab === 'pivoted'

  return (
    <>
      <PageHeader title="Đề tài qua các kỳ" />

      {/* Tabs & Controls Bar */}
      <div className="mb-4 flex flex-wrap items-center justify-between gap-4 border-b border-border pb-3">
        <div role="tablist" aria-label="Bộ lọc đề tài" className="flex gap-2">
          <Button
            role="tab"
            aria-selected={activeTab === 'all'}
            variant={activeTab === 'all' ? 'outline' : 'text'}
            onClick={() => setTab('all')}
          >
            Tất cả đề tài
          </Button>
          <Button
            role="tab"
            aria-selected={activeTab === 'pivoted'}
            variant={activeTab === 'pivoted' ? 'outline' : 'text'}
            onClick={() => setTab('pivoted')}
          >
            Đổi hướng đề tài
          </Button>
          {isAdmin && (
            <Button
              role="tab"
              aria-selected={activeTab === 'unlinked'}
              variant={activeTab === 'unlinked' ? 'outline' : 'text'}
              onClick={() => setTab('unlinked')}
            >
              Chưa gắn đề tài
              {unlinkedTeamsQuery.data && unlinkedTeamsQuery.data.length > 0 && (
                <span className="ml-1.5 rounded-full bg-warning-container px-2 py-0.5 text-xs font-semibold text-warning">
                  {unlinkedTeamsQuery.data.length}
                </span>
              )}
            </Button>
          )}
        </div>

        {/* Học kỳ Selector */}
        <div className="flex items-center gap-2">
          <label htmlFor="semester-filter" className="text-caption font-medium text-text-secondary">
            Học kỳ:
          </label>
          <select
            id="semester-filter"
            value={selectedSemesterId}
            onChange={(e) => setSemester(e.target.value)}
            className="rounded-md border border-border bg-surface px-3 py-1.5 text-body text-text-primary shadow-sm focus:border-primary focus:outline-none"
          >
            <option value="">Tất cả học kỳ</option>
            {semestersQuery.data?.map((sem) => (
              <option key={sem.id} value={sem.id}>
                {sem.name} ({sem.code})
              </option>
            ))}
          </select>
        </div>
      </div>

      {semestersQuery.isError && (
        <div className="mb-4">
          <ErrorState error={semestersQuery.error} onRetry={() => void semestersQuery.refetch()} />
        </div>
      )}

      {/* Content depending on active tab */}
      {activeTab !== 'unlinked' ? (
        <DataTable
          queryKey={projectKeys.list(
            selectedSemesterId || undefined,
            undefined,
            isPivoted,
            { page: 1, pageSize: 20, filters: {} },
          )}
          fetchPage={(query) =>
            fetchProjects(
              query,
              selectedSemesterId || undefined,
              query.filters.domain,
              isPivoted,
            )
          }
          columns={columns}
          searchPlaceholder="Tìm theo tên hoặc mô tả đề tài..."
          empty={
            <EmptyState
              icon="history_edu"
              title={isPivoted ? 'Không có đề tài đổi hướng' : 'Chưa có đề tài nào'}
              description="Các đề tài liên kết qua các kỳ EXE101 và EXE201 sẽ hiển thị tại đây."
            />
          }
        />
      ) : (
        <div className="space-y-4">
          <div className="rounded-md bg-info-container p-4 text-info-dark">
            <div className="flex items-start gap-3">
              <Icon name="info" className="text-info-dark" />
              <div>
                <p className="font-semibold">Danh sách nhóm EXE201 chưa liên kết đề tài</p>
                <p className="text-caption text-text-primary">
                  Đây là các nhóm kế thừa từ EXE101 hoặc nhóm mới cần được gắn thủ công vào chuỗi đề tài tương ứng.
                  Chọn nhóm và chỉ định đề tài để thiết lập cây phả hệ.
                </p>
              </div>
            </div>
          </div>

          {!selectedSemesterId ? (
            <div className="rounded-md border border-dashed border-border p-8 text-center">
              <Icon name="event" size={48} className="mx-auto text-text-disabled" />
              <p className="mt-2 font-medium text-text-primary">Vui lòng chọn học kỳ</p>
              <p className="text-caption text-text-secondary">
                Chọn một học kỳ từ danh sách phía trên để xem các nhóm chưa gắn đề tài.
              </p>
            </div>
          ) : unlinkedTeamsQuery.isLoading ? (
            <div className="rounded-lg border border-border bg-surface">
              <TableSkeleton columns={5} rows={4} />
            </div>
          ) : unlinkedTeamsQuery.isError ? (
            <ErrorState
              error={unlinkedTeamsQuery.error}
              onRetry={() => void unlinkedTeamsQuery.refetch()}
            />
          ) : unlinkedTeamsQuery.data?.length === 0 ? (
            <EmptyState
              icon="check_circle"
              title="Tất cả nhóm đã được gắn đề tài"
              description="Không có nhóm EXE201 nào trong học kỳ này chưa liên kết đề tài."
            />
          ) : (
            <div className="overflow-hidden rounded-lg border border-border bg-surface">
              <table className="w-full text-left text-body">
                <thead className="border-b border-border bg-neutral-container/50 text-caption font-semibold text-text-secondary">
                  <tr>
                    <th className="px-4 py-3">Nhóm</th>
                    <th className="px-4 py-3">Trưởng nhóm</th>
                    <th className="px-4 py-3">Số thành viên</th>
                    <th className="px-4 py-3">Lĩnh vực</th>
                    <th className="px-4 py-3 text-right">Thao tác</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-border">
                  {unlinkedTeamsQuery.data?.map((team) => (
                    <tr key={team.id} className="hover:bg-neutral-container/20">
                      <td className="px-4 py-3">
                        <div className="font-medium text-text-primary">{team.name}</div>
                        <div className="text-caption text-text-secondary">{team.courseCode}</div>
                      </td>
                      <td className="px-4 py-3">
                        {team.leader ? (
                          <div>
                            <div className="font-medium text-text-primary">{team.leader.fullName}</div>
                            <div className="text-caption text-text-secondary">{team.leader.studentCode}</div>
                          </div>
                        ) : (
                          <span className="text-text-secondary">—</span>
                        )}
                      </td>
                      <td className="px-4 py-3 text-text-primary">{team.memberCount} thành viên</td>
                      <td className="px-4 py-3">
                        <div className="flex flex-wrap gap-1">
                          {team.domains && team.domains.length > 0 ? (
                            team.domains.map((d) => (
                              <span
                                key={d.id}
                                className="rounded bg-neutral-container px-2 py-0.5 text-caption text-text-secondary"
                              >
                                {d.name || d.code}
                              </span>
                            ))
                          ) : (
                            <span className="text-text-secondary">—</span>
                          )}
                        </div>
                      </td>
                      <td className="px-4 py-3 text-right">
                        <Button
                          variant="primary"
                          icon="link"
                          onClick={() => handleOpenLinkModal(team)}
                        >
                          Gắn vào đề tài
                        </Button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      )}

      {/* Modal Lịch sử phiên bản đề tài */}
      {selectedProjectForHistory && (
        <Dialog
          open={true}
          onClose={() => setSelectedProjectForHistory(null)}
          title={`Lịch sử đề tài: ${selectedProjectForHistory.name}`}
          actions={
            <Button variant="outline" onClick={() => setSelectedProjectForHistory(null)}>
              Đóng
            </Button>
          }
        >
          <div className="max-h-[60vh] space-y-4 overflow-y-auto pr-1">
            {selectedProjectForHistory.versions && selectedProjectForHistory.versions.length > 0 ? (
              <div className="relative ml-2 space-y-4 border-l-2 border-primary/30 pl-4">
                {selectedProjectForHistory.versions.map((ver: ProjectVersion) => (
                  <div key={ver.version} className="relative">
                    {/* Dot */}
                    <span className="absolute -left-[23px] top-1.5 size-3 rounded-full border-2 border-surface bg-primary" />
                    <div>
                      <div className="flex items-center gap-2">
                        <span className="font-semibold text-text-primary">
                          Phiên bản {ver.version}: {ver.name}
                        </span>
                        <StatusChip
                          tone={
                            ver.changeType === 'PIVOT' || ver.changeType === 'CHANGE'
                              ? 'warning'
                              : 'info'
                          }
                        >
                          {ver.changeType}
                        </StatusChip>
                      </div>
                      {ver.description && (
                        <p className="mt-1 text-caption text-text-secondary">{ver.description}</p>
                      )}
                      {ver.reason && (
                        <div className="mt-1 rounded bg-neutral-container p-2 text-caption text-text-primary">
                          <span className="font-medium">Lý do: </span>
                          {ver.reason}
                        </div>
                      )}
                      <div className="mt-1 flex flex-wrap gap-3 text-caption text-text-disabled">
                        {ver.semesterCode && <span>Kỳ: {ver.semesterCode}</span>}
                        {ver.changedByName && <span>Người đổi: {ver.changedByName}</span>}
                        <span>{formatDate(ver.createdAt)}</span>
                      </div>
                    </div>
                  </div>
                ))}
              </div>
            ) : (
              <p className="text-caption text-text-secondary">Chưa có thông tin lịch sử thay đổi.</p>
            )}
          </div>
        </Dialog>
      )}

      {/* Modal Gắn nhóm vào đề tài */}
      {teamToLink && (
        <Dialog
          open={true}
          onClose={() => setTeamToLink(null)}
          title={`Gắn nhóm "${teamToLink.name}" vào đề tài`}
          actions={
            <>
              <Button
                variant="outline"
                disabled={linkTeamMutation.isPending}
                onClick={() => setTeamToLink(null)}
              >
                Hủy
              </Button>
              <Button
                variant="primary"
                loading={linkTeamMutation.isPending}
                disabled={!targetProjectId || !!linkSuccess}
                onClick={handleConfirmLink}
              >
                Xác nhận gắn
              </Button>
            </>
          }
        >
          <div className="space-y-4">
            <p className="text-body text-text-secondary">
              Nhập hoặc chọn ID đề tài xuyên kỳ mà nhóm này kế thừa:
            </p>

            <div>
              <label htmlFor="target-project-id" className="block text-caption font-medium text-text-primary">
                Mã ID Đề tài (GUID):
              </label>
              <input
                id="target-project-id"
                type="text"
                placeholder="Ví dụ: b1234567-89ab-cdef-0123-456789abcdef"
                value={targetProjectId}
                onChange={(e) => setTargetProjectId(e.target.value.trim())}
                className="mt-1 w-full rounded-md border border-border bg-surface px-3 py-2 text-body text-text-primary focus:border-primary focus:outline-none"
              />
            </div>

            {linkError && (
              <Banner tone="error" title="Lỗi gắn đề tài">
                {linkError}
              </Banner>
            )}

            {linkSuccess && (
              <Banner tone="success" title="Thành công">
                {linkSuccess}
              </Banner>
            )}
          </div>
        </Dialog>
      )}
    </>
  )
}
