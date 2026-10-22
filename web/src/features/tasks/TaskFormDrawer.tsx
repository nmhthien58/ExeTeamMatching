import { useId, useState, type FormEvent, type ReactNode } from 'react'
import { toApiError } from '@/shared/api/apiClient'
import { Banner, Button } from '@/shared/components/ui'
import { Drawer } from '@/features/workspace/components'
import { useWorkspaceContext } from '@/features/workspace/context'
import { useCreateTask, useUpdateTask, type TaskDetail, type TaskInput } from './api'

/** `2026-10-22T08:00` cho ô datetime-local theo giờ máy. */
function toLocalInput(iso?: string | null) {
  if (!iso) return ''
  const d = new Date(iso)
  return new Date(d.getTime() - d.getTimezoneOffset() * 60_000).toISOString().slice(0, 16)
}

/**
 * Tạo / sửa việc (bottom sheet tạo việc của C04 – drawer trên web). Giảng viên hướng dẫn tạo bài nộp (bắt buộc hạn).
 * Lỗi hiện tại từng ô; lỗi từ server (fieldErrors) gắn đúng ô.
 */
export function TaskFormDrawer({ task, onClose }: { task?: TaskDetail; onClose: () => void }) {
  const { workspace } = useWorkspaceContext()
  const isDeliverable = task ? task.kind === 'DELIVERABLE' : workspace.myRole === 'INSTRUCTOR'
  const students = (workspace.people ?? []).filter((p) => p.personType === 'STUDENT')
  const create = useCreateTask(workspace.id)
  const update = useUpdateTask(task ?? { id: '', workspaceId: workspace.id })
  const mutation = task ? update : create

  const [title, setTitle] = useState(task?.title ?? '')
  const [description, setDescription] = useState(task?.description ?? '')
  const [dueAt, setDueAt] = useState(toLocalInput(task?.dueAt))
  const [assignees, setAssignees] = useState<string[]>(task?.assignees.map((a) => a.personId) ?? [])
  const [errors, setErrors] = useState<Record<string, string>>({})

  const serverError = mutation.error ? toApiError(mutation.error) : null
  const fieldError = (key: string) => errors[key] ?? serverError?.fieldErrors[key]?.[0]

  const submit = (e: FormEvent) => {
    e.preventDefault()
    const next: Record<string, string> = {}
    if (!title.trim()) next.title = 'Nhập tên việc.'
    else if (title.trim().length > 200) next.title = 'Tên việc tối đa 200 ký tự.'
    if (description.length > 4000) next.description = 'Mô tả tối đa 4.000 ký tự.'
    if (isDeliverable && !dueAt) next.dueAt = 'Bài nộp phải có hạn nộp.'
    setErrors(next)
    if (Object.keys(next).length > 0) return

    const input: TaskInput = {
      title: title.trim(),
      description: description.trim() || null,
      kind: isDeliverable ? 'DELIVERABLE' : 'TASK',
      assigneeStudentIds: assignees,
      dueAt: dueAt ? new Date(dueAt).toISOString() : null,
    }
    mutation.mutate(input, { onSuccess: onClose })
  }

  const heading = task ? (isDeliverable ? 'Sửa bài nộp' : 'Sửa việc') : isDeliverable ? 'Tạo bài nộp' : 'Tạo việc'
  return (
    <Drawer
      open
      onClose={onClose}
      title={heading}
      footer={
        <>
          <Button variant="outline" onClick={onClose}>
            Hủy
          </Button>
          <Button type="submit" form="task-form" loading={mutation.isPending}>
            {task ? 'Lưu thay đổi' : heading}
          </Button>
        </>
      }
    >
      <form id="task-form" onSubmit={submit} noValidate className="flex flex-col gap-4">
        {serverError && !Object.keys(serverError.fieldErrors).length && <Banner tone="error" title={serverError.title}>{serverError.detail}</Banner>}

        <Field label="Tên việc" error={fieldError('title')}>
          {(id, describedBy) => (
            <input id={id} aria-describedby={describedBy} aria-invalid={!!fieldError('title')} value={title} maxLength={200}
              onChange={(e) => setTitle(e.target.value)} className={inputClass(!!fieldError('title'))} />
          )}
        </Field>

        <Field label="Mô tả (không bắt buộc)" error={fieldError('description')}>
          {(id, describedBy) => (
            <textarea id={id} aria-describedby={describedBy} rows={4} value={description} maxLength={4000}
              onChange={(e) => setDescription(e.target.value)} className={`${inputClass(!!fieldError('description'))} py-2`} />
          )}
        </Field>

        <Field label={isDeliverable ? 'Hạn nộp' : 'Hạn (không bắt buộc)'} error={fieldError('dueAt')}>
          {(id, describedBy) => (
            <input id={id} type="datetime-local" aria-describedby={describedBy} aria-invalid={!!fieldError('dueAt')} value={dueAt}
              onChange={(e) => setDueAt(e.target.value)} className={inputClass(!!fieldError('dueAt'))} />
          )}
        </Field>

        <fieldset>
          <legend className="mb-2 font-medium">Người làm (không bắt buộc)</legend>
          {students.length === 0 ? (
            <p className="text-body text-text-secondary">Chưa tải được danh sách thành viên.</p>
          ) : (
            <div className="flex flex-col gap-1">
              {students.map((s) => (
                <label key={s.personId} className="flex min-h-10 items-center gap-3">
                  <input
                    type="checkbox"
                    className="size-5 accent-secondary"
                    checked={assignees.includes(s.personId)}
                    onChange={(e) =>
                      setAssignees((current) => (e.target.checked ? [...current, s.personId] : current.filter((id) => id !== s.personId)))
                    }
                  />
                  {s.displayName}
                </label>
              ))}
            </div>
          )}
          {fieldError('assigneeStudentIds') && <p className="mt-1 text-body text-error">{fieldError('assigneeStudentIds')}</p>}
        </fieldset>
      </form>
    </Drawer>
  )
}

const inputClass = (invalid: boolean) =>
  `min-h-10 w-full rounded-sm border bg-surface px-3 ${invalid ? 'border-2 border-error' : 'border-border-strong'}`

/** Nhãn phía trên ô, dòng lỗi màu Error bên dưới (DESIGN.md 6.1, 8.1). */
export function Field({ label, error, children }: {
  label: string
  error?: string
  children: (id: string, describedBy: string | undefined) => ReactNode
}) {
  const id = useId()
  return (
    <div className="flex flex-col gap-2">
      <label htmlFor={id} className="font-medium">
        {label}
      </label>
      {children(id, error ? `${id}-error` : undefined)}
      {error && (
        <p id={`${id}-error`} className="text-body text-error">
          {error}
        </p>
      )}
    </div>
  )
}
