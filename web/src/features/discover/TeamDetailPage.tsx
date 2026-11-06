import { useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, ApiError } from '@/shared/api/apiClient'
import { ErrorState } from '@/shared/components/States'
import { Banner, Button, StatusChip } from '@/shared/components/ui'
import { PageHeader } from '@/shared/layout/PageHeader'
import { Avatar, Card, Drawer } from '@/features/workspace/components'
import { useSnackbar } from '@/features/workspace/useSnackbar'
import { useMyTeam } from '@/features/suggestions/api'
import { DomainChips, TeamRequirementBar } from '@/features/suggestions/components'
import { refreshTeams, teamStatusLabel, type FullTeam, type JoinRequest } from '@/features/my_team/api'
import { CancelJoinRequestDialog } from '@/features/invitations/InvitationsPage'
import { linkClass, ListSkeleton } from '@/features/my_team/components'
import { useJoinRequest, useTeam } from './api'

/**
 * S08 – Chi tiết nhóm xem từ bên ngoài (FE-37, cùng spec MB-16): tên + trạng thái, domain, ý tưởng, điều kiện, thành viên + chuyên ngành
 * (không liên hệ), bài tuyển; nút chính theo quan hệ. S09 mở trong drawer phải.
 */
export function TeamDetailPage() {
  const { teamId = '' } = useParams()
  const team = useTeam(teamId)
  return (
    <>
      <PageHeader title="Chi tiết nhóm" />
      {team.error ? <ErrorState error={team.error} onRetry={() => void team.refetch()} /> : team.isLoading ? <ListSkeleton /> : <Detail team={team.data!} />}
    </>
  )
}

function Detail({ team }: { team: FullTeam }) {
  const status = teamStatusLabel[team.status]
  return (
    <div className="grid gap-4 lg:grid-cols-[2fr_1fr]">
      <div className="flex flex-col gap-4">
        <Card>
          <div className="flex flex-wrap items-center justify-between gap-2">
            <h2 className="text-headline font-semibold">{team.name}</h2>
            <StatusChip tone={status.tone} icon={status.icon}>
              {status.text}
            </StatusChip>
          </div>
          {team.projectName && <p className="mt-1 text-title">{team.projectName}</p>}
          <div className="mt-2">
            <DomainChips domains={team.domains} />
          </div>
          {team.ideaDescription && <p className="mt-3">{team.ideaDescription}</p>}
          <div className="mt-3">
            <TeamRequirementBar memberCount={team.memberCount} maxSize={team.requirement?.maxSize ?? 6} showOpenSlots />
          </div>
        </Card>
        <Card>
          <h3 className="mb-2 font-semibold">Thành viên</h3>
          <ul className="flex flex-col gap-2">
            {team.members.map((m) => (
              <li key={m.id} className="flex items-center gap-3">
                <Avatar name={m.fullName} size={32} />
                <span className="flex-1">
                  {m.fullName}
                  {m.specialization?.name && <span className="text-text-secondary"> · {m.specialization.name}</span>}
                </span>
                {m.role === 'LEADER' && <StatusChip tone="info">Trưởng nhóm</StatusChip>}
              </li>
            ))}
          </ul>
        </Card>
        {team.recruitmentPost && (
          <Card>
            <h3 className="font-semibold">Đang tuyển: {team.recruitmentPost.title}</h3>
            {team.recruitmentPost.description && <p className="text-text-secondary">{team.recruitmentPost.description}</p>}
          </Card>
        )}
      </div>
      <Card className="self-start">
        <Action team={team} />
      </Card>
    </div>
  )
}

/** Nút chính theo quan hệ (flutter-handoff S08); nút khóa luôn kèm lý do. */
function Action({ team }: { team: FullTeam }) {
  const myTeam = useMyTeam()
  const snackbar = useSnackbar()
  const [joining, setJoining] = useState(false)
  const full = team.memberCount >= (team.requirement?.maxSize ?? 6)
  const reason = (text: string) => <p className="text-text-secondary">{text}</p>

  const content = (() => {
    switch (team.myRelation) {
      case 'LEADER':
      case 'MEMBER':
        return (
          <Link to="/sv/nhom" className={`${linkClass} border border-border-strong text-info-dark`}>
            Đây là nhóm của bạn
          </Link>
        )
      case 'REQUESTED':
        return <Requested team={team} onCancelled={() => snackbar.show('Đã hủy yêu cầu')} />
      case 'INVITED':
        return (
          <Link to="/sv/loi-moi" className={`${linkClass} bg-primary text-white`}>
            Xem lời mời
          </Link>
        )
    }
    if (myTeam.data) return reason('Bạn đã có nhóm.')
    if (full) return reason(`Nhóm đã đủ ${team.requirement?.maxSize ?? 6} người.`)
    if (team.status !== 'DRAFT' && team.status !== 'ACTIVE') return reason('Nhóm không còn nhận thành viên.')
    return (
      <Button className="w-full" onClick={() => setJoining(true)}>
        Xin tham gia
      </Button>
    )
  })()

  return (
    <>
      {content}
      {joining && (
        <JoinDrawer
          teamId={team.id}
          onClose={() => setJoining(false)}
          onSent={() => {
            setJoining(false)
            snackbar.show('Đã gửi yêu cầu')
          }}
        />
      )}
      {snackbar.element}
    </>
  )
}

/** Đã gửi yêu cầu đang chờ: trạng thái + Hủy yêu cầu (hỏi xác nhận); mã yêu cầu lấy từ `GET /api/join-requests/me`. */
function Requested({ team, onCancelled }: { team: FullTeam; onCancelled: () => void }) {
  const queryClient = useQueryClient()
  const [confirming, setConfirming] = useState(false)
  const mine = useQuery({ queryKey: ['join-requests', 'me'], queryFn: async () => (await api.get<JoinRequest[]>('/api/join-requests/me')).data })
  const pending = mine.data?.find((r) => r.team?.id === team.id && r.status === 'PENDING')
  const cancel = useMutation({
    mutationFn: async (id: string) => api.post(`/api/join-requests/${id}/cancel`),
    onSuccess: () => {
      setConfirming(false)
      onCancelled()
    },
    onSettled: () => refreshTeams(queryClient),
  })
  const error = cancel.error instanceof ApiError ? cancel.error : null
  return (
    <div className="flex flex-col gap-3">
      <StatusChip tone="warning" icon="schedule">
        Đã gửi yêu cầu · chờ trưởng nhóm
      </StatusChip>
      {error && <Banner tone="error" title={error.title}>{error.detail}</Banner>}
      <Button variant="outline" className="w-full" disabled={!pending || cancel.isPending} onClick={() => setConfirming(true)}>
        Hủy yêu cầu
      </Button>
      <CancelJoinRequestDialog
        open={confirming}
        teamName={team.name}
        loading={cancel.isPending}
        onClose={() => setConfirming(false)}
        onConfirm={() => pending && cancel.mutate(pending.id)}
      />
    </div>
  )
}

/**
 * S09 – Gửi yêu cầu tham gia (bản 4): lời nhắn, 1–3 ý tưởng (tiêu đề ≤ 80, mô tả ≤ 300) hoặc "Chưa có ý tưởng" (quy tắc 17),
 * nhắc hạn 72 giờ. Lỗi gửi không đóng drawer và giữ nội dung.
 */
export function JoinDrawer({ teamId, onClose, onSent }: { teamId: string; onClose: () => void; onSent: () => void }) {
  const join = useJoinRequest(teamId)
  const [message, setMessage] = useState('')
  const [ideas, setIdeas] = useState<{ title: string; description: string }[]>([])
  const [noIdea, setNoIdea] = useState(false)
  const [missing, setMissing] = useState(false)
  const error = join.error instanceof ApiError ? join.error : null
  const setIdea = (i: number, patch: Partial<{ title: string; description: string }>) => setIdeas(ideas.map((x, j) => (j === i ? { ...x, ...patch } : x)))

  const send = () => {
    const filled = noIdea ? [] : ideas.filter((i) => i.title.trim()).map((i) => ({ title: i.title.trim(), description: i.description.trim() || undefined }))
    if (!noIdea && filled.length === 0) {
      setMissing(true)
      return
    }
    setMissing(false)
    join.mutate({ message: message.trim() || undefined, ideas: filled, hasNoIdea: noIdea }, { onSuccess: onSent })
  }

  return (
    <Drawer
      open
      onClose={onClose}
      title="Xin tham gia"
      footer={
        <>
          <Button variant="text" onClick={onClose} disabled={join.isPending}>
            Hủy
          </Button>
          <Button loading={join.isPending} onClick={send}>
            Gửi yêu cầu
          </Button>
        </>
      }
    >
      <div className="flex flex-col gap-4">
        {error && (
          <Banner tone="error" title={error.title}>
            {error.detail}
          </Banner>
        )}
        <label className="flex flex-col gap-2">
          <span className="font-medium">Lời nhắn cho trưởng nhóm</span>
          <textarea rows={3} maxLength={500} value={message} onChange={(e) => setMessage(e.target.value)} className="rounded-sm border border-border-strong px-3 py-2" />
        </label>
        <fieldset className="flex flex-col gap-3" aria-describedby={missing ? 'ideas-error' : undefined}>
          <legend className="mb-2 font-medium">Ý tưởng của bạn</legend>
          {!noIdea &&
            ideas.map((idea, i) => (
              <div key={i} className="flex flex-col gap-2 rounded-md border border-border p-3">
                <div className="flex gap-2">
                  <input
                    aria-label={`Ý tưởng ${i + 1}`}
                    placeholder={`Ý tưởng ${i + 1}`}
                    maxLength={80}
                    value={idea.title}
                    onChange={(e) => setIdea(i, { title: e.target.value })}
                    className="min-w-0 flex-1 rounded-sm border border-border-strong px-3 py-2"
                  />
                  <Button variant="text" aria-label={`Xóa ý tưởng ${i + 1}`} icon="delete" onClick={() => setIdeas(ideas.filter((_, j) => j !== i))} />
                </div>
                <textarea
                  aria-label={`Mô tả ý tưởng ${i + 1}`}
                  placeholder="Mô tả ngắn"
                  rows={2}
                  maxLength={300}
                  value={idea.description}
                  onChange={(e) => setIdea(i, { description: e.target.value })}
                  className="rounded-sm border border-border-strong px-3 py-2"
                />
              </div>
            ))}
          {!noIdea && ideas.length < 3 && (
            <Button variant="text" icon="add" className="self-start" onClick={() => setIdeas([...ideas, { title: '', description: '' }])}>
              Thêm ý tưởng
            </Button>
          )}
          <label className="flex min-h-12 items-center gap-3">
            <input
              type="checkbox"
              className="size-5"
              checked={noIdea}
              onChange={(e) => {
                setNoIdea(e.target.checked)
                setMissing(false)
              }}
            />
            Chưa có ý tưởng
          </label>
          {missing && (
            <p id="ideas-error" role="alert" className="text-caption text-error">
              Thêm ít nhất 1 ý tưởng hoặc chọn "Chưa có ý tưởng".
            </p>
          )}
        </fieldset>
        <p className="text-text-secondary">Yêu cầu hết hạn sau 72 giờ nếu trưởng nhóm chưa trả lời.</p>
      </div>
    </Drawer>
  )
}
