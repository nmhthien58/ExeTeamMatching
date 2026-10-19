import { PageHeader } from '@/shared/layout/PageHeader'
import { EmptyState } from './States'

/**
 * Chỗ giữ cho màn hình chưa làm: hiện mã màn hình và người phụ trách để biết route nào thuộc ai.
 * Người làm thay component này bằng màn hình thật trong `src/features/<feature>/`.
 */
export function ScreenPlaceholder({ screen, title, owner }: { screen: string; title: string; owner: string }) {
  return (
    <>
      <PageHeader title={title} />
      <div className="rounded-lg bg-surface shadow-elevation-1">
        <EmptyState
          icon="construction"
          title={`${screen} · ${title}`}
          description={`Màn hình đang được làm – người phụ trách: ${owner}. Spec ở docs/ux (danh sách màn hình, flutter-handoff, user-flow).`}
        />
      </div>
    </>
  )
}
