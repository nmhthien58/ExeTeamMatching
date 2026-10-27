import { useState, type ReactNode } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { ApiError } from '@/shared/api/apiClient'
import { EmptyState, ErrorState } from '@/shared/components/States'
import { Banner, Button, Dialog, Icon } from '@/shared/components/ui'
import { PageHeader } from '@/shared/layout/PageHeader'
import { Card } from '@/features/workspace/components'
import { useSnackbar } from '@/features/workspace/useSnackbar'
import { useInvite, useMyTeam, useSpecializations, type CatalogRef, type StudentRef } from '@/features/suggestions/api'
import { StudentCard } from '@/features/suggestions/components'
import { InviteDialog } from '@/features/suggestions/InviteDialog'
import { useDomains } from '@/features/my_team/api'
import { Field, ListSkeleton } from '@/features/my_team/components'
import { postErrors, useMySeekingPost, useSaveMySeeking, useSeekingPosts, type SeekingPost } from './postsApi'

/**
 * S10 – tab "Sinh viên tìm nhóm" của Khám phá (FE-37, cùng spec MB-17): bài của mình (→ S11), lọc chuyên ngành, phân trang.
 * Trưởng nhóm còn chỗ có nút "Mời vào nhóm" (design-decisions B2).
 */
// shortcut: chạm thẻ chưa mở S04 (hồ sơ sinh viên khác – Nam); nối link khi S04 có trên web.
export function SeekingPostsPanel() {
  const [specializationId, setSpecializationId] = useState<number>()
  const [page, setPage] = useState(1)
  const posts = useSeekingPosts(specializationId, page)
  const team = useMyTeam()
  const specializations = useSpecializations().data ?? []
  const domains = new Map((useDomains().data ?? []).map((d) => [d.id, d]))
  const invite = useInvite(team.data?.id)
  const snackbar = useSnackbar()
  const [inviting, setInviting] = useState<StudentRef | null>(null)
  const [invited, setInvited] = useState<Set<string>>(new Set())
  const maxSize = team.data?.requirement?.maxSize ?? 6
  const canInvite =
    team.data?.myRelation === 'LEADER' && (team.data.status === 'DRAFT' || team.data.status === 'ACTIVE') && team.data.memberCount < maxSize

  return (
    <div className="flex flex-col gap-4">
      {team.data === null && <MyPostCard />}
      <label className="flex max-w-xs flex-col gap-1">
        <span className="text-caption text-text-secondary">Chuyên ngành</span>
        <select
          value={specializationId ?? ''}
          onChange={(e) => {
            setSpecializationId(e.target.value ? Number(e.target.value) : undefined)
            setPage(1)
          }}
          className="h-10 rounded-sm border border-border-strong bg-surface px-3"
        >
          <option value="">Tất cả chuyên ngành</option>
          {specializations.map((s) => (
            <option key={s.id} value={s.id}>
              {s.name}
            </option>
          ))}
        </select>
      </label>
      {posts.error ? (
        <ErrorState error={posts.error} onRetry={() => void posts.refetch()} />
      ) : posts.isLoading ? (
        <ListSkeleton />
      ) : posts.data!.items.length === 0 ? (
        <EmptyState icon="person_search" title="Chưa có sinh viên đang tìm nhóm" description="Thử chuyên ngành khác hoặc quay lại sau." />
      ) : (
        <>
          <div className="grid gap-3 md:grid-cols-2">
            {posts.data!.items.map((p) => (
              <SeekingPostCard
                key={p.id}
                post={p}
                domains={p.domainIds.flatMap((id) => domains.get(id) ?? [])}
                action={
                  canInvite &&
                  (invited.has(p.student.id) ? (
                    <span className="inline-flex items-center gap-1 text-success">
                      <Icon name="check" size={20} /> Đã mời
                    </span>
                  ) : (
                    <Button variant="outline" icon="person_add" onClick={() => setInviting(p.student)}>
                      Mời vào nhóm
                    </Button>
                  ))
                }
              />
            ))}
          </div>
          {posts.data!.totalPages > 1 && (
            <nav aria-label="Phân trang" className="flex items-center justify-center gap-3">
              <Button variant="text" disabled={page <= 1} onClick={() => setPage(page - 1)}>
                Trước
              </Button>
              <span>
                Trang {page}/{posts.data!.totalPages}
              </span>
              <Button variant="text" disabled={page >= posts.data!.totalPages} onClick={() => setPage(page + 1)}>
                Sau
              </Button>
            </nav>
          )}
        </>
      )}
      <InviteDialog
        key={inviting?.id}
        student={inviting}
        sending={invite.isPending}
        error={invite.error}
        onClose={() => {
          setInviting(null)
          invite.reset()
        }}
        onSend={(message) =>
          invite.mutate(
            { studentId: inviting!.id, message: message || undefined },
            {
              onSuccess: () => {
                setInvited((s) => new Set(s).add(inviting!.id))
                setInviting(null)
                snackbar.show('Đã gửi lời mời')
              },
            },
          )
        }
      />
      {snackbar.element}
    </div>
  )
}

function MyPostCard() {
  const mine = useMySeekingPost()
  return (
    <Card className="p-0">
      <Link to="/sv/kham-pha/tim-nhom" className="flex min-h-12 items-center gap-3 px-4 py-3 hover:bg-background">
        <Icon name={mine.data ? 'check_circle' : 'campaign'} />
        <span className="flex-1">
          <span className="block font-semibold">{mine.data ? 'Bạn đang tìm nhóm' : 'Đăng bài "Tôi đang tìm nhóm"'}</span>
          <span className="text-text-secondary">{mine.data?.title ?? 'Trưởng nhóm thấy vai trò bạn muốn và có thể mời bạn.'}</span>
        </span>
        <span className="font-medium text-info-dark">{mine.data ? 'Sửa' : 'Đăng bài'}</span>
      </Link>
    </Card>
  )
}

/** Thẻ bài tìm nhóm: họ tên, MSSV · chuyên ngành, vai trò, có thể đóng góp, kỹ năng, domain quan tâm. */
export function SeekingPostCard({ post, domains, action }: { post: SeekingPost; domains: CatalogRef[]; action?: ReactNode }) {
  return (
    <StudentCard student={post.student} skills={post.skills.map((s) => `${s.skill} ${s.level}/5`)} action={action}>
      <p>
        <span className="font-medium">Vai trò:</span> {post.title}
      </p>
      {post.description && <p className="line-clamp-3 text-text-secondary">{post.description}</p>}
      {domains.length > 0 && (
        <span className="flex flex-wrap gap-2">
          {domains.map((d) => (
            <span key={d.id} title={d.name} className="inline-flex h-8 items-center rounded-full bg-info-container px-3 text-info-dark">
              {d.code}
            </span>
          ))}
        </span>
      )}
    </StudentCard>
  )
}

/** S11 – Đăng / sửa bài "Tôi đang tìm nhóm" (FE-37, cùng spec MB-17): nạp bài hiện có rồi mở form. */
export function SeekingPostFormPage() {
  const mine = useMySeekingPost()
  if (mine.error) return <ErrorState error={mine.error} onRetry={() => void mine.refetch()} />
  if (mine.isLoading) return <ListSkeleton />
  return <SeekingPostForm post={mine.data ?? null} />
}

function SeekingPostForm({ post }: { post: SeekingPost | null }) {
  const navigate = useNavigate()
  const save = useSaveMySeeking()
  const domains = useDomains().data ?? []
  const [title, setTitle] = useState(post?.title ?? '')
  const [description, setDescription] = useState(post?.description ?? '')
  const [domainIds, setDomainIds] = useState<number[]>(post?.domainIds ?? [])
  const [submitted, setSubmitted] = useState(false)
  const [confirming, setConfirming] = useState(false)
  const apiError = save.error instanceof ApiError ? save.error : null
  const titleError = apiError?.fieldErrors.title?.[0] ?? (submitted && !title.trim() ? 'Nhập vai trò mong muốn.' : undefined)
  const done = () => navigate('/sv/kham-pha', { replace: true })

  const submit = () => {
    setSubmitted(true)
    if (!title.trim()) return
    save.mutate({ kind: 'save', input: { title: title.trim(), description: description.trim() || undefined, domainIds } }, { onSuccess: done })
  }

  return (
    <>
      <PageHeader title={post ? 'Sửa bài tìm nhóm' : 'Tôi đang tìm nhóm'} />
      <Card className="flex max-w-2xl flex-col gap-4">
        {apiError && Object.keys(apiError.fieldErrors).length === 0 && (
          <Banner tone="error" title={postErrors[apiError.code] ?? apiError.title}>
            {apiError.detail}
          </Banner>
        )}
        <Field label="Vai trò mong muốn" value={title} onChange={setTitle} error={titleError} maxLength={150} hint="Ví dụ: Lập trình mobile, thiết kế UI" />
        <Field label="Bạn có thể đóng góp gì" value={description} onChange={setDescription} maxLength={2000} rows={4} error={apiError?.fieldErrors.description?.[0]} />
        <fieldset>
          <legend className="mb-2 font-medium">Domain quan tâm</legend>
          <div className="flex flex-wrap gap-2">
            {domains.map((d) => {
              const selected = domainIds.includes(d.id)
              return (
                <button
                  key={d.id}
                  type="button"
                  aria-pressed={selected}
                  title={d.name}
                  onClick={() => setDomainIds(selected ? domainIds.filter((id) => id !== d.id) : [...domainIds, d.id])}
                  className={`min-h-10 rounded-full border px-3 ${selected ? 'border-primary bg-primary-container text-primary-dark' : 'border-border-strong'}`}
                >
                  {selected && '✓ '}
                  {d.code}
                </button>
              )
            })}
          </div>
        </fieldset>
        <p className="text-text-secondary">Kỹ năng lấy theo hồ sơ của bạn.</p>
        <div className="flex flex-wrap justify-end gap-2">
          {post && (
            <Button variant="text" icon="delete" onClick={() => setConfirming(true)}>
              Gỡ bài
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
        title="Gỡ bài tìm nhóm?"
        actions={
          <>
            <Button variant="text" onClick={() => setConfirming(false)}>
              Hủy
            </Button>
            <Button variant="danger" loading={save.isPending} onClick={() => save.mutate({ kind: 'remove' }, { onSuccess: done })}>
              Gỡ bài
            </Button>
          </>
        }
      >
        Trưởng nhóm sẽ không thấy bạn trong danh sách Sinh viên tìm nhóm nữa.
      </Dialog>
    </>
  )
}
