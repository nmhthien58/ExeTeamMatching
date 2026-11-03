import { Link } from 'react-router-dom'
import { useMyTeam } from '@/features/suggestions/api'
import { EmptyState, ErrorState } from '@/shared/components/States'
import { PageHeader } from '@/shared/layout/PageHeader'
import { useTeamLecturer, useTeamPreferences } from './api'
import { lecturerReasonLabel } from '@/features/my_team/advisorsApi'
import { StatusChip } from '@/shared/components/ui'

export function TeamLecturerPage() {
  const team = useMyTeam()
  const teamId = team.data?.id
  const assignment = useTeamLecturer(teamId)
  const preferences = useTeamPreferences(teamId)
  if (team.error) return <ErrorState error={team.error} onRetry={() => void team.refetch()} />
  if (team.isLoading) return <p role="status" className="p-4">Đang tải nhóm…</p>
  if (!teamId) return <EmptyState title="Bạn chưa có nhóm" />
  if (assignment.isLoading || preferences.isLoading) return <p role="status" className="p-4">Đang tải cố vấn…</p>
  if (assignment.error) return <ErrorState error={assignment.error} onRetry={() => void assignment.refetch()} />
  if (preferences.error) return <ErrorState error={preferences.error} onRetry={() => void preferences.refetch()} />
  return <><PageHeader title="Giảng viên của nhóm" />{assignment.data ? <section className="max-w-2xl rounded-xl border border-border bg-surface p-5"><h2 className="text-title font-semibold">{assignment.data.lecturerName}</h2><div className="mt-2"><StatusChip tone="info">{lecturerReasonLabel(assignment.data.reason, assignment.data.preferenceRank) ?? 'Đã ghép'}</StatusChip></div><p className="mt-4 text-caption text-text-secondary">Nhóm đã được ghép; nguyện vọng đã khóa.</p></section> : <section className="max-w-2xl rounded-xl border border-border bg-surface p-5"><h2 className="text-title font-semibold">Chưa ghép giảng viên</h2>{preferences.data?.lockAt && <p className="mt-2">Hạn chốt: {new Intl.DateTimeFormat('vi-VN', { dateStyle: 'short', timeStyle: 'short' }).format(new Date(preferences.data.lockAt))}</p>}<ol className="mt-4 space-y-2">{(preferences.data?.preferences ?? []).map((lecturer) => <li key={lecturer.lecturerId} className="rounded-lg border border-border p-3"><span className="font-medium">Nguyện vọng {lecturer.rank}: {lecturer.fullName}</span>{lecturer.isInterested && <span className="ml-2 rounded-full bg-info-container px-2 py-1 text-caption text-info-dark">Quan tâm nhóm bạn</span>}</li>)}</ol>{preferences.data?.preferences.length === 0 && <p className="text-text-secondary">Nhóm chưa chọn nguyện vọng.</p>}<Link to="/sv/nhom/giang-vien" className="mt-4 inline-block text-primary-dark underline">Xem và sửa nguyện vọng</Link></section>}</>
}
