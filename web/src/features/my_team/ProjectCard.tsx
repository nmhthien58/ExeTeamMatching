import { useState } from 'react'
import { EmptyState } from '@/shared/components/States'
import { Button, Icon } from '@/shared/components/ui'
import { Drawer } from '@/features/workspace/components'
import { formatDateTime } from '@/features/workspace/format'
import type { FullTeam } from './api'
import { describeVersion, useTeamProject } from './projectApi'

/**
 * Thẻ đề tài ở S15 (MB-45, bản web; DESIGN.md 6.3): tên đề tài, "Tiếp tục từ EXE101 · SP26" khi nhóm EXE201 kế thừa,
 * nút "Lịch sử" mở dòng thời gian đề tài trong drawer. Chưa có đề tài hoặc tải lỗi thì giữ tên đề tài của thẻ nhóm.
 */
export function ProjectCard({ team }: { team: FullTeam }) {
  const project = useTeamProject(team.id).data
  const [open, setOpen] = useState(false)
  const name = project?.name ?? team.projectName
  if (!name) return null
  const continued = !!project && team.courseCode === 'EXE201' && project.originCourseCode === 'EXE101'
  return (
    <div className="mt-2">
      <p className="text-title">{name}</p>
      {continued && (
        <p className="mt-1 flex items-center gap-2 text-info-dark">
          <Icon name="history" size={20} />
          {['Tiếp tục từ EXE101', project.originSemesterCode].filter(Boolean).join(' · ')}
        </p>
      )}
      {project && project.versions.length > 0 && (
        <Button variant="text" icon="timeline" className="mt-1 px-2" onClick={() => setOpen(true)}>
          Lịch sử
        </Button>
      )}
      {project && (
        <Drawer open={open} onClose={() => setOpen(false)} title="Lịch sử đề tài">
          {project.versions.length === 0 ? (
            <EmptyState icon="history" title="Chưa có thay đổi" />
          ) : (
            <ol aria-label="Dòng thời gian đề tài" className="flex flex-col">
              {project.versions.map((v, i) => (
                <li key={v.version} className="relative flex gap-3 pb-4">
                  <span className="flex flex-col items-center">
                    <span className={`mt-1.5 size-3 rounded-full ${i === 0 ? 'bg-primary' : 'bg-border-strong'}`} aria-hidden />
                    {i < project.versions.length - 1 && <span className="w-0.5 flex-1 bg-border" aria-hidden />}
                  </span>
                  <span className="flex-1">
                    <span className="block text-caption font-semibold text-text-secondary">
                      {v.semesterCode} · {v.courseCode}
                    </span>
                    <span className="block">{describeVersion(v, project.versions[i + 1])}</span>
                    <span className="text-caption text-text-secondary">{[v.changedByName, formatDateTime(v.createdAt)].filter(Boolean).join(' · ')}</span>
                  </span>
                </li>
              ))}
            </ol>
          )}
        </Drawer>
      )}
    </div>
  )
}
