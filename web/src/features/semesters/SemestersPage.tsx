import { useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { Button } from '@/shared/components/ui'
import { PageHeader } from '@/shared/layout/PageHeader'
import { RoundForm, type RoundFormTarget } from '@/features/rounds/RoundForm'
import { RoundsTab } from '@/features/rounds/RoundsTab'
import type { Semester } from './api'
import { SemesterForm } from './SemesterForm'
import { SemestersTab } from './SemestersTab'

/** Mục "Học kỳ và đợt": W03 và W04 trên một trang, tab lưu trên URL (`?tab=dot`). */
export function SemestersPage() {
  const [params, setParams] = useSearchParams()
  const tab = params.get('tab') === 'dot' ? 'dot' : 'hoc-ky'
  const [semesterForm, setSemesterForm] = useState<{ semester: Semester | null } | null>(null)
  const [roundForm, setRoundForm] = useState<RoundFormTarget | null>(null)
  const [formKey, setFormKey] = useState(0)

  const openSemester = (semester: Semester | null) => {
    setFormKey((k) => k + 1)
    setSemesterForm({ semester })
  }
  const openRound = (target: RoundFormTarget) => {
    setFormKey((k) => k + 1)
    setRoundForm(target)
  }

  const action =
    tab === 'hoc-ky' ? (
      <Button icon="add" onClick={() => openSemester(null)}>
        Tạo học kỳ
      </Button>
    ) : (
      <Button icon="add" onClick={() => openRound({ round: null, semesterId: params.get('hk') ?? undefined })}>
        Tạo đợt
      </Button>
    )

  return (
    <>
      <PageHeader title="Học kỳ và đợt" action={action} />
      <div role="tablist" aria-label="Học kỳ và đợt" className="mb-4 flex gap-2">
        {[
          { value: 'hoc-ky', label: 'Học kỳ' },
          { value: 'dot', label: 'Đợt đăng ký' },
        ].map((t) => (
          <Button
            key={t.value}
            role="tab"
            aria-selected={tab === t.value}
            variant={tab === t.value ? 'outline' : 'text'}
            onClick={() => setParams(t.value === 'dot' ? { tab: 'dot' } : {}, { replace: true })}
          >
            {t.label}
          </Button>
        ))}
      </div>

      {tab === 'hoc-ky' ? (
        <SemestersTab onCreate={() => openSemester(null)} onEdit={openSemester} />
      ) : (
        <RoundsTab onCreate={openRound} onEdit={openRound} />
      )}

      {semesterForm && <SemesterForm key={formKey} semester={semesterForm.semester} onClose={() => setSemesterForm(null)} />}
      {roundForm && <RoundForm key={formKey} target={roundForm} onClose={() => setRoundForm(null)} />}
    </>
  )
}
