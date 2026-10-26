import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { ApiError } from '@/shared/api/apiClient'
import { EmptyState, ErrorState } from '@/shared/components/States'
import { Banner, Button } from '@/shared/components/ui'
import { PageHeader } from '@/shared/layout/PageHeader'
import { Card, Drawer } from '@/features/workspace/components'
import { useMyTeam, type CatalogRef } from '@/features/suggestions/api'
import { useDomains, useSaveTeam, type FullTeam } from './api'
import { Field, ListSkeleton } from './components'

/** S16 – sửa thông tin nhóm: nạp nhóm hiện tại rồi mở form. */
export function EditTeamPage() {
  const team = useMyTeam()
  if (team.error) return <ErrorState error={team.error} onRetry={() => void team.refetch()} />
  if (team.isLoading) return <ListSkeleton />
  if (!team.data) return <EmptyState icon="group_off" title="Bạn chưa có nhóm" />
  return <TeamFormPage team={team.data as FullTeam} />
}

/**
 * S13 Tạo nhóm / S16 Sửa thông tin (FE-37, cùng spec MB-19, MB-21): tên nhóm, đề tài, ý tưởng, domain (S14 – drawer chọn nhiều,
 * domain đầu là domain chính). Lỗi theo ô từ ProblemDetails; lỗi chung → banner, giữ nội dung.
 */
export function TeamFormPage({ team }: { team?: FullTeam }) {
  const navigate = useNavigate()
  const save = useSaveTeam(team?.id)
  const [name, setName] = useState(team?.name ?? '')
  const [projectName, setProjectName] = useState(team?.projectName ?? '')
  const [idea, setIdea] = useState(team?.ideaDescription ?? '')
  const [domains, setDomains] = useState<CatalogRef[]>(team?.domains ?? [])
  const [picking, setPicking] = useState(false)
  const [submitted, setSubmitted] = useState(false)
  const apiError = save.error instanceof ApiError ? save.error : null
  const fieldError = (field: string) => apiError?.fieldErrors[field]?.[0]
  const nameError = fieldError('name') ?? (submitted && name.trim().length < 2 ? 'Tên nhóm cần ít nhất 2 ký tự.' : undefined)
  const domainError = fieldError('domainIds') ?? (submitted && domains.length === 0 ? 'Chọn ít nhất một domain.' : undefined)

  const submit = () => {
    setSubmitted(true)
    if (name.trim().length < 2 || domains.length === 0) return
    save.mutate(
      { name: name.trim(), projectName: projectName.trim(), ideaDescription: idea.trim(), domainIds: domains.map((d) => d.id), primaryDomainId: domains[0].id },
      { onSuccess: () => navigate('/sv/nhom', { replace: true }) },
    )
  }

  return (
    <>
      <PageHeader title={team ? 'Sửa thông tin' : 'Tạo nhóm'} />
      <Card className="flex max-w-2xl flex-col gap-4">
        {apiError && Object.keys(apiError.fieldErrors).length === 0 && (
          <Banner tone="error" title={apiError.title} action={<Button variant="text" onClick={submit}>Thử lại</Button>}>
            {apiError.detail}
          </Banner>
        )}
        <Field label="Tên nhóm" value={name} onChange={setName} error={nameError} maxLength={100} />
        <Field
          label="Tên đề tài"
          value={projectName}
          onChange={setProjectName}
          maxLength={200}
          disabled={team?.isInherited}
          error={fieldError('projectName')}
          hint={team?.isInherited ? 'Đổi đề tài qua yêu cầu đổi nhóm.' : 'Có thể bổ sung sau.'}
        />
        <Field label="Mô tả ý tưởng" value={idea} onChange={setIdea} maxLength={4000} rows={4} />
        <div className="flex flex-col gap-2">
          <span className="font-medium">Domain</span>
          <div className="flex flex-wrap gap-2">
            {domains.map((d, i) => (
              <span key={d.id} className="inline-flex h-8 items-center gap-1 rounded-full bg-info-container pl-3 pr-1 text-info-dark">
                {d.code} {d.name}
                {i === 0 && ' (chính)'}
                <button type="button" aria-label={`Bỏ ${d.code}`} className="grid size-8 place-items-center rounded-full hover:bg-white/50" onClick={() => setDomains(domains.filter((x) => x.id !== d.id))}>
                  ×
                </button>
              </span>
            ))}
            <Button variant="outline" icon="add" onClick={() => setPicking(true)}>
              Chọn domain
            </Button>
          </div>
          {domainError && <span className="text-caption text-error">{domainError}</span>}
        </div>
        <div className="flex justify-end gap-2">
          <Button variant="text" onClick={() => navigate(-1)}>
            Hủy
          </Button>
          <Button loading={save.isPending} onClick={submit}>
            {team ? 'Lưu' : 'Tạo nhóm'}
          </Button>
        </div>
      </Card>
      <DomainDrawer open={picking} selected={domains} onClose={() => setPicking(false)} onPick={(picked) => {
          setDomains(picked)
          setPicking(false)
        }}
      />
    </>
  )
}

/** S14 – chọn domain D01–D18 có tìm kiếm. */
function DomainDrawer({ open, selected, onClose, onPick }: { open: boolean; selected: CatalogRef[]; onClose: () => void; onPick: (d: CatalogRef[]) => void }) {
  const domains = useDomains()
  const [picked, setPicked] = useState(selected)
  const [search, setSearch] = useState('')
  const toggle = (d: CatalogRef) => setPicked(picked.some((p) => p.id === d.id) ? picked.filter((p) => p.id !== d.id) : [...picked, d])
  const shown = (domains.data ?? []).filter((d) => `${d.code} ${d.name}`.toLowerCase().includes(search.toLowerCase()))
  return (
    <Drawer
      open={open}
      onClose={onClose}
      title="Chọn domain"
      footer={
        <>
          <Button variant="text" onClick={onClose}>
            Hủy
          </Button>
          <Button onClick={() => onPick(picked)}>Chọn ({picked.length})</Button>
        </>
      }
    >
      <input
        aria-label="Tìm domain"
        placeholder="Tìm domain"
        value={search}
        onChange={(e) => setSearch(e.target.value)}
        className="mb-3 w-full rounded-sm border border-border-strong px-3 py-2"
      />
      {domains.error ? (
        <ErrorState error={domains.error} onRetry={() => void domains.refetch()} />
      ) : (
        shown.map((d) => (
          <label key={d.id} className="flex min-h-12 items-center gap-3">
            <input type="checkbox" className="size-5" checked={picked.some((p) => p.id === d.id)} onChange={() => toggle(d)} />
            {d.code} · {d.name}
          </label>
        ))
      )}
    </Drawer>
  )
}
