import { Link, useParams } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { api } from '@/shared/api/apiClient'
import { ErrorState } from '@/shared/components/States'
import { Button } from '@/shared/components/ui'
import { PageHeader } from '@/shared/layout/PageHeader'
import { useMyTeam } from '@/features/suggestions/api'
import { useSavePreferences, useTeamPreferences, type LecturerCard } from './api'

export function LecturerDetailPage() {
  const { lecturerId = '' } = useParams()
  const team = useMyTeam()
  const detail = useQuery({ queryKey: ['lecturer', lecturerId], queryFn: async () => (await api.get<LecturerCard>(`/api/lecturers/${lecturerId}`)).data, enabled: !!lecturerId })
  const preferences = useTeamPreferences(team.data?.id)
  const save = useSavePreferences(team.data?.id ?? '')
  if (detail.isLoading || preferences.isLoading) return <p role="status" className="p-4">Đang tải giảng viên…</p>
  if (detail.error) return <ErrorState error={detail.error} onRetry={() => void detail.refetch()} />
  if (preferences.error) return <ErrorState error={preferences.error} onRetry={() => void preferences.refetch()} />
  const lecturer = detail.data
  if (!lecturer) return <p>Không tìm thấy giảng viên.</p>
  const current = preferences.data?.preferences ?? []
  const included = current.some((item) => item.lecturerId === lecturerId)
  const canEdit = team.data?.myRelation === 'LEADER' && !preferences.data?.locked
  return <><PageHeader title="Chi tiết giảng viên" /><section className="max-w-2xl rounded-xl border border-border bg-surface p-5"><h2 className="text-headline font-semibold">{lecturer.fullName}</h2><p className="mt-3 font-medium">Chuyên môn</p><ul className="mt-1 flex flex-wrap gap-2">{lecturer.domains.map((domain) => <li key={domain.id} className="rounded-full bg-info-container px-3 py-1 text-caption text-info-dark">{domain.name}</li>)}</ul>{preferences.data?.interestedLecturers.some((item) => item.lecturerId === lecturerId) && <p className="mt-4 rounded-lg bg-info-container p-3 text-info-dark">Đang quan tâm nhóm bạn</p>}{canEdit && <Button className="mt-5" disabled={save.isPending || (!included && current.length >= (preferences.data?.maxPreferences ?? 3))} onClick={() => save.mutate(included ? current.filter((item) => item.lecturerId !== lecturerId).map((item) => item.lecturerId) : [...current.map((item) => item.lecturerId), lecturerId])}>{included ? 'Bỏ khỏi nguyện vọng' : 'Thêm vào nguyện vọng'}</Button>}{save.error && <p role="alert" className="mt-3 text-error">Không lưu được. Vui lòng thử lại.</p>}<Link to="/sv/nhom/giang-vien" className="mt-4 inline-block text-primary-dark underline">Quay lại nguyện vọng</Link></section></>
}
