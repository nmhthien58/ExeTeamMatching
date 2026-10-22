import clsx from 'clsx'
import { Icon } from '@/shared/components/ui'
import type { CourseCode } from '@/shared/phase/usePhase'
import { COURSES } from './adminApi'

/** Chọn môn EXE101 / EXE201 (chip lọc: đang chọn nền Secondary, chữ trắng, có dấu tích). */
export function CourseToggle({ value, onChange }: { value: CourseCode; onChange: (course: CourseCode) => void }) {
  return (
    <div role="radiogroup" aria-label="Môn" className="flex gap-2">
      {COURSES.map((course) => (
        <button
          key={course}
          type="button"
          role="radio"
          aria-checked={value === course}
          onClick={() => onChange(course)}
          className={clsx(
            'inline-flex min-h-10 items-center gap-1 rounded-full px-4 font-medium',
            value === course ? 'bg-secondary text-white' : 'border border-border-strong bg-surface hover:bg-background',
          )}
        >
          {value === course && <Icon name="check" size={20} />}
          {course}
        </button>
      ))}
    </div>
  )
}
