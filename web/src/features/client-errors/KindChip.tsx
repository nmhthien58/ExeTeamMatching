import { StatusChip } from '@/shared/components/ui'
import type { ClientErrorKind } from './api'

export function KindChip({ kind }: { kind: ClientErrorKind }) {
  return kind === 'fatal' ? (
    <StatusChip tone="error" icon="dangerous">
      Fatal
    </StatusChip>
  ) : (
    <StatusChip tone="info" icon="report">
      Non-fatal
    </StatusChip>
  )
}
