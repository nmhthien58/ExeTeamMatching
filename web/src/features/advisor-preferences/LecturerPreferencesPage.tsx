import { useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { useMyTeam } from '@/features/suggestions/api'
import { EmptyState, ErrorState } from '@/shared/components/States'
import { Button, Icon } from '@/shared/components/ui'
import { PageHeader } from '@/shared/layout/PageHeader'
import { useLecturers, useSavePreferences, useTeamPreferences } from './api'

export function LecturerPreferencesPage() {
  const team = useMyTeam()
  const teamId = team.data?.id
  const preferences = useTeamPreferences(teamId)
  const save = useSavePreferences(teamId ?? '')
  const [search, setSearch] = useState('')
  const lecturers = useLecturers(search)

  const ids = useMemo(() => preferences.data?.preferences.map((item) => item.lecturerId) ?? [], [preferences.data])
  const ordered = preferences.data?.preferences ?? []

  const update = (next: string[]) => save.mutate(next)
  const add = (id: string) => { if (!ids.includes(id) && ids.length < (preferences.data?.maxPreferences ?? 3)) update([...ids, id]) }
  const remove = (id: string) => update(ids.filter((item) => item !== id))
  const move = (index: number, direction: -1 | 1) => {
    const target = index + direction
    if (target < 0 || target >= ids.length) return
    const next = [...ids]
    ;[next[index], next[target]] = [next[target], next[index]]
    update(next)
  }

  if (team.isLoading) return <p role="status" className="p-4">Đang tải nhóm…</p>
  if (team.error) return <ErrorState error={team.error} onRetry={() => void team.refetch()} />
  if (!teamId || !team.data) return <EmptyState title="Bạn chưa có nhóm" description="Nguyện vọng giảng viên chỉ dành cho thành viên nhóm." />
  const isLeader = team.data.myRelation === 'LEADER'

  return (
    <>
      <PageHeader title="Nguyện vọng giảng viên" />
      {preferences.isLoading ? <p role="status">Đang tải nguyện vọng…</p> : preferences.error ? <ErrorState error={preferences.error} onRetry={() => void preferences.refetch()} /> : preferences.data && (
        <div className="grid gap-4 xl:grid-cols-[minmax(0,1fr)_minmax(300px,0.8fr)]">
          <section className="rounded-xl border border-border bg-surface p-4" aria-labelledby="preferences-title">
            <div className="flex flex-wrap items-start justify-between gap-3">
              <div><h2 id="preferences-title" className="text-title font-semibold">Thứ tự nguyện vọng</h2><p className="mt-1 text-caption text-text-secondary">Chọn tối đa {preferences.data.maxPreferences} giảng viên. Chỉ trưởng nhóm được sửa.</p></div>
              {preferences.data.lockAt && <p className="text-caption text-text-secondary">Chốt lúc {new Intl.DateTimeFormat('vi-VN', { dateStyle: 'short', timeStyle: 'short' }).format(new Date(preferences.data.lockAt))}</p>}
            </div>
            {preferences.data.locked && <p className="mt-3 rounded-lg bg-info-container p-3 text-info-dark">Nguyện vọng đã khóa sau khi kết thúc đợt hoặc ghép giảng viên.</p>}
            {ordered.length === 0 ? <p className="mt-4 text-text-secondary">Nhóm chưa chọn nguyện vọng.</p> : (
              <ol className="mt-3 space-y-2" aria-label="Danh sách nguyện vọng có thứ tự">
                {ordered.map((lecturer, index) => (
                  <li key={lecturer.lecturerId} className="flex items-center gap-3 rounded-lg border border-border p-3">
                    <span className="grid size-9 place-items-center rounded-full bg-primary-container font-semibold text-primary-dark">{index + 1}</span>
                    <span className="min-w-0 flex-1"><Link className="font-medium text-primary-dark underline" to={`/sv/nhom/giang-vien/${lecturer.lecturerId}`}>{lecturer.fullName}</Link><span className="block text-caption text-text-secondary">{lecturer.isInterested ? 'Quan tâm nhóm bạn' : lecturer.domains.map((d) => d.name).join(' · ')}</span></span>
                    {!preferences.data.locked && isLeader && <span className="flex gap-1"><Button variant="text" aria-label={`Đưa ${lecturer.fullName} lên`} disabled={index === 0 || save.isPending} onClick={() => move(index, -1)}><Icon name="arrow_upward" /></Button><Button variant="text" aria-label={`Đưa ${lecturer.fullName} xuống`} disabled={index === ordered.length - 1 || save.isPending} onClick={() => move(index, 1)}><Icon name="arrow_downward" /></Button><Button variant="text" aria-label={`Bỏ ${lecturer.fullName}`} disabled={save.isPending} onClick={() => remove(lecturer.lecturerId)}>Bỏ</Button></span>}
                  </li>
                ))}
              </ol>
            )}
            {preferences.data.previousInstructor && !ids.includes(preferences.data.previousInstructor.lecturerId) && (
              <div className="mt-4 rounded-lg bg-info-container p-3"><p className="font-medium">Đã hướng dẫn ở EXE101</p><p>{preferences.data.previousInstructor.fullName}</p>{!preferences.data.locked && isLeader && <Button variant="text" onClick={() => add(preferences.data!.previousInstructor!.lecturerId)}>Thêm vào nguyện vọng</Button>}</div>
            )}
            {preferences.data.interestedLecturers.length > 0 && <div className="mt-4"><h3 className="font-semibold">Quan tâm nhóm bạn</h3><ul className="mt-2 space-y-2">{preferences.data.interestedLecturers.map((lecturer) => <li key={lecturer.lecturerId} className="flex items-center justify-between rounded-lg bg-background p-3"><span>{lecturer.fullName}</span>{!preferences.data!.locked && isLeader && !ids.includes(lecturer.lecturerId) && <Button variant="outline" disabled={save.isPending || ids.length >= preferences.data!.maxPreferences} onClick={() => add(lecturer.lecturerId)}>Thêm vào nguyện vọng</Button>}</li>)}</ul></div>}
            {save.error && <p role="alert" className="mt-3 text-error">Không lưu được nguyện vọng. Vui lòng thử lại.</p>}
          </section>
          <section className="rounded-xl border border-border bg-surface p-4" aria-labelledby="lecturers-title">
            <h2 id="lecturers-title" className="text-title font-semibold">Danh sách giảng viên</h2>
            <label className="mt-3 block"><span className="sr-only">Tìm giảng viên</span><input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Tìm theo tên giảng viên" className="w-full rounded-lg border border-border bg-surface px-3 py-2" /></label>
            {lecturers.isLoading ? <p role="status" className="mt-3">Đang tải giảng viên…</p> : lecturers.error ? <ErrorState error={lecturers.error} onRetry={() => void lecturers.refetch()} /> : <ul className="mt-3 divide-y divide-border">{(lecturers.data ?? []).map((lecturer) => <li key={lecturer.id} className="flex items-center justify-between gap-3 py-3"><span><Link className="font-medium text-primary-dark underline" to={`/sv/nhom/giang-vien/${lecturer.id}`}>{lecturer.fullName}</Link><span className="block text-caption text-text-secondary">{lecturer.domains.map((d) => d.name).join(' · ') || 'Chưa có chuyên môn'}</span></span>{!preferences.data!.locked && team.data!.myRelation === 'LEADER' && <Button variant={ids.includes(lecturer.id) ? 'outline' : 'text'} disabled={save.isPending || (!ids.includes(lecturer.id) && ids.length >= preferences.data!.maxPreferences)} onClick={() => ids.includes(lecturer.id) ? remove(lecturer.id) : add(lecturer.id)}>{ids.includes(lecturer.id) ? 'Bỏ nguyện vọng' : 'Thêm'}</Button>}</li>)}</ul>}
          </section>
        </div>
      )}
    </>
  )
}
