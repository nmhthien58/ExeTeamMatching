import clsx from 'clsx'
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
 * Trưởng nhóm còn chỗ có nút "Mời vào nhóm" (design-decisions B2); "Xem hồ sơ" mở S04.
 */
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
      <div role="group" aria-label="Lọc theo chuyên ngành" className="flex flex-wrap gap-2">
        {[{ id: undefined as number | undefined, name: 'Tất cả chuyên ngành' }, ...specializations].map((sp) => {
          const selected = specializationId === sp.id
          return (
            <button
              key={sp.id ?? 'all'}
              type="button"
              aria-pressed={selected}
              onClick={() => {
                setSpecializationId(sp.id)
                setPage(1)
              }}
              className={clsx(
                'min-h-9 rounded-full border px-3.5 text-caption font-medium transition-colors',
                selected ? 'border-ink bg-ink text-white' : 'border-border bg-surface text-text-secondary hover:border-primary/60 hover:text-text-primary',
              )}
            >
              {sp.name}
            </button>
          )
        })}
      </div>
      {posts.error ? (
        <ErrorState error={posts.error} onRetry={() => void posts.refetch()} />
      ) : posts.isLoading ? (
        <ListSkeleton />
      ) : posts.data!.items.length === 0 ? (
        <EmptyState icon="person_search" title="Chưa có sinh viên đang tìm nhóm" description="Thử chuyên ngành khác hoặc quay lại sau." />
      ) : (
        <>
          <div className="grid gap-4 md:grid-cols-2 2xl:grid-cols-3">
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
    <Link
      to="/sv/kham-pha/tim-nhom"
      className="card-hover group relative flex items-center gap-4 overflow-hidden rounded-lg bg-ink p-5 text-white shadow-elevation-2"
    >
      <span aria-hidden className="pointer-events-none absolute -right-16 -top-20 size-56 rounded-full bg-brand-orange/30 blur-3xl" />
      <span aria-hidden className="relative grid size-12 shrink-0 place-items-center rounded-[14px] bg-gradient-to-br from-brand-orange to-primary shadow-glow">
        <Icon name={mine.data ? 'check_circle' : 'campaign'} filled className="text-white" />
      </span>
      <span className="relative min-w-0 flex-1">
        <span className="block text-subtitle font-semibold">{mine.data ? 'Bạn đang tìm nhóm' : 'Đăng bài "Tôi đang tìm nhóm"'}</span>
        <span className="block truncate text-caption text-ink-text">{mine.data?.title ?? 'Trưởng nhóm thấy vai trò bạn muốn và có thể mời bạn.'}</span>
      </span>
      <span className="relative inline-flex min-h-10 items-center gap-1 rounded-md bg-white/10 px-4 font-semibold transition-colors group-hover:bg-white/20">
        {mine.data ? 'Sửa' : 'Đăng bài'}
        <Icon name="arrow_forward" size={20} />
      </span>
    </Link>
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
      <Link
        to={`/sv/sinh-vien/${post.student.id}`}
        aria-label={`Xem hồ sơ ${post.student.fullName}`}
        className="inline-flex min-h-10 items-center gap-1 self-start font-medium text-info-dark"
      >
        Xem hồ sơ
        <Icon name="chevron_right" size={20} />
      </Link>
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
