import type { TaskCard } from '@/features/tasks/api'

const DUE_SOON_DAYS = 3

/** Số liệu của 4 thẻ tính từ bảng việc. */
export function taskStats(tasks: TaskCard[], myPersonId: string | undefined, now: Date = new Date()) {
  const open = tasks.filter((t) => t.status !== 'DONE' && !t.isArchive)
  const soon = now.getTime() + DUE_SOON_DAYS * 24 * 60 * 60 * 1000
  return {
    mine: myPersonId ? open.filter((t) => t.assignees.some((a) => a.personId === myPersonId)).length : 0,
    // Việc đã trễ không tính là "sắp đến hạn" (đã có lọc Trễ hạn riêng ở C04).
    dueSoon: open.filter((t) => t.dueAt && new Date(t.dueAt).getTime() >= now.getTime() && new Date(t.dueAt).getTime() <= soon).length,
    pendingReview: tasks.filter((t) => t.kind === 'DELIVERABLE' && t.submissionStatus === 'PENDING').length,
  }
}
