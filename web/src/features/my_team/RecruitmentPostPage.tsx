import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { ApiError } from '@/shared/api/apiClient'
import { EmptyState, ErrorState } from '@/shared/components/States'
import { Banner, Button, Dialog, Icon } from '@/shared/components/ui'
import { PageHeader } from '@/shared/layout/PageHeader'
import { Card } from '@/features/workspace/components'
import { useMyTeam, useSpecializations } from '@/features/suggestions/api'
import { postErrors, useSaveRecruitment } from '@/features/discover/postsApi'
import type { FullTeam } from './api'
import { Field, ListSkeleton } from './components'

/** S21 – Đăng / sửa bài tuyển thành viên (FE-37, cùng spec MB-25): nạp nhóm rồi mở form; chỉ trưởng nhóm. */
export function RecruitmentPostPage() {
  const team = useMyTeam()
  if (team.error) return <ErrorState error={team.error} onRetry={() => void team.refetch()} />
  if (team.isLoading) return <ListSkeleton />
  if (team.data?.myRelation !== 'LEADER') return <EmptyState icon="lock" title="Chỉ trưởng nhóm đăng bài tuyển" />
  return <RecruitmentForm team={team.data as FullTeam} />
}

/** Tiêu đề, chuyên ngành cần tìm, mô tả yêu cầu; số chỗ trống theo sĩ số; Đóng bài khi đang có bài mở. */
function RecruitmentForm({ team }: { team: FullTeam }) {
  const navigate = useNavigate()
  const post = team.recruitmentPost
  const save = useSaveRecruitment(team.id)
  const specializations = useSpecializations().data ?? []
  const [title, setTitle] = useState(post?.title ?? '')
  const [description, setDescription] = useState(post?.description ?? '')
  const [specializationIds, setSpecializationIds] = useState<number[]>(post?.specializationIds ?? [])
  const [submitted, setSubmitted] = useState(false)
  const [confirming, setConfirming] = useState(false)
  const apiError = save.error instanceof ApiError ? save.error : null
  const titleError = apiError?.fieldErrors.title?.[0] ?? (submitted && !title.trim() ? 'Nhập tiêu đề bài tuyển.' : undefined)
  const maxSize = team.requirement?.maxSize ?? 6
  const done = () => navigate('/sv/nhom', { replace: true })

  const submit = () => {
    setSubmitted(true)
    if (!title.trim()) return
    save.mutate({ kind: 'save', input: { title: title.trim(), description: description.trim() || undefined, specializationIds } }, { onSuccess: done })
  }

  return (
    <>
      <PageHeader title={post ? 'Sửa bài tuyển' : 'Đăng bài tuyển'} />
      <Card className="flex max-w-2xl flex-col gap-4">
        {apiError && Object.keys(apiError.fieldErrors).length === 0 && (
          <Banner tone="error" title={postErrors[apiError.code] ?? apiError.title}>
            {apiError.detail}
          </Banner>
        )}
        <p className="flex items-center gap-2">
          <Icon name="event_seat" />
          <span>
            <span className="font-semibold">Còn {maxSize - team.memberCount} chỗ trống</span>
            <span className="block text-text-secondary">
              {team.memberCount}/{maxSize} thành viên – tự cập nhật khi có người vào nhóm.
            </span>
          </span>
        </p>
        <Field label="Tiêu đề" value={title} onChange={setTitle} error={titleError} maxLength={150} hint="Ví dụ: Cần 1 bạn Marketing" />
        <fieldset>
          <legend className="mb-2 font-medium">Chuyên ngành cần tìm</legend>
          <div className="flex flex-wrap gap-2">
            {specializations.map((s) => {
              const selected = specializationIds.includes(s.id)
              return (
                <button
                  key={s.id}
                  type="button"
                  aria-pressed={selected}
                  onClick={() => setSpecializationIds(selected ? specializationIds.filter((id) => id !== s.id) : [...specializationIds, s.id])}
                  className={`min-h-10 rounded-full border px-3 ${selected ? 'border-primary bg-primary-container text-primary-dark' : 'border-border-strong'}`}
                >
                  {selected && '✓ '}
                  {s.name}
                </button>
              )
            })}
          </div>
          {apiError?.fieldErrors.specializationIds?.[0] && <span className="text-caption text-error">{apiError.fieldErrors.specializationIds[0]}</span>}
        </fieldset>
        <Field label="Mô tả yêu cầu" value={description} onChange={setDescription} maxLength={2000} rows={4} error={apiError?.fieldErrors.description?.[0]} />
        <div className="flex flex-wrap justify-end gap-2">
          {post && (
            <Button variant="text" icon="campaign" onClick={() => setConfirming(true)}>
              Đóng bài
            </Button>
          )}
          <Button variant="text" onClick={() => navigate(-1)}>
            Hủy
          </Button>
          <Button loading={save.isPending} onClick={submit}>
            {post ? 'Lưu' : 'Đăng bài'}
          </Button>
        </div>
      </Card>
      <Dialog
        open={confirming}
        onClose={() => setConfirming(false)}
        title="Đóng bài tuyển?"
        actions={
          <>
            <Button variant="text" onClick={() => setConfirming(false)}>
              Hủy
            </Button>
            <Button variant="danger" loading={save.isPending} onClick={() => save.mutate({ kind: 'close' }, { onSuccess: done })}>
              Đóng bài
            </Button>
          </>
        }
      >
        Nhóm sẽ không còn hiện mục "Đang tuyển" ở trang chi tiết nhóm.
      </Dialog>
    </>
  )
}
