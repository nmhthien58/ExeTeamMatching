import { useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { EmptyState, ErrorState, TableSkeleton } from '@/shared/components/States'
import { Button, StatusChip } from '@/shared/components/ui'
import { PageHeader } from '@/shared/layout/PageHeader'
import { useSnackbar } from '@/features/workspace/useSnackbar'
import { catalogKinds, useCatalog, type CatalogItem, type CatalogKind } from './api'
import { CatalogDrawer } from './CatalogDrawer'

const tabs = Object.keys(catalogKinds) as CatalogKind[]
const th = 'h-11 px-4 text-left text-caption font-semibold text-text-secondary'

/** Mục "Danh mục": W12 Mã lớp (FE-18), W13 Domain (FE-19), W14 Chuyên ngành (FE-20) trên một trang, tab lưu trên URL (`?tab=`). */
export function CatalogsPage() {
  const [params, setParams] = useSearchParams()
  const kind = tabs.find((t) => t === params.get('tab')) ?? 'class-codes'
  const rules = catalogKinds[kind]
  const [editing, setEditing] = useState<CatalogItem | null | undefined>(undefined)
  const snackbar = useSnackbar()

  return (
    <>
      <PageHeader title="Danh mục" action={<Button icon="add" onClick={() => setEditing(null)}>Thêm {rules.noun}</Button>} />
      <div role="tablist" aria-label="Danh mục" className="mb-4 flex gap-2">
        {tabs.map((t) => (
          <Button
            key={t}
            role="tab"
            aria-selected={kind === t}
            variant={kind === t ? 'outline' : 'text'}
            onClick={() => {
              setEditing(undefined)
              setParams(t === 'class-codes' ? {} : { tab: t }, { replace: true })
            }}
          >
            {catalogKinds[t].tab}
          </Button>
        ))}
      </div>
      <CatalogTable key={kind} kind={kind} onCreate={() => setEditing(null)} onEdit={setEditing} />
      {editing !== undefined && (
        <CatalogDrawer
          key={`${kind}-${editing?.id ?? 'new'}`}
          kind={kind}
          item={editing}
          onClose={() => setEditing(undefined)}
          onSaved={(message) => {
            setEditing(undefined)
            snackbar.show(message)
          }}
        />
      )}
      {snackbar.element}
    </>
  )
}

function CatalogTable({ kind, onCreate, onEdit }: { kind: CatalogKind; onCreate: () => void; onEdit: (item: CatalogItem) => void }) {
  const rules = catalogKinds[kind]
  const items = useCatalog(kind)

  if (items.isError) return <ErrorState error={items.error} onRetry={() => void items.refetch()} />
  return (
    <div className="overflow-x-auto rounded-lg bg-surface shadow-elevation-1">
      {items.isLoading ? (
        <TableSkeleton columns={4} />
      ) : !items.data?.length ? (
        <EmptyState icon="category" title={`Chưa có ${rules.noun}`} action={<Button onClick={onCreate}>Thêm {rules.noun}</Button>} />
      ) : (
        <table className="w-full border-collapse text-body">
          <thead className="bg-background">
            <tr>
              <th scope="col" className={th}>Mã</th>
              {rules.hasName && <th scope="col" className={th}>Tên</th>}
              {rules.hasDescription && <th scope="col" className={th}>Lĩnh vực con, ngành chính</th>}
              <th scope="col" className={th}>Trạng thái</th>
              <th scope="col" className={th}><span className="sr-only">Thao tác</span></th>
            </tr>
          </thead>
          <tbody>
            {items.data.map((item) => (
              <tr key={item.id} className="border-t border-border">
                <td className="h-[52px] px-4 font-medium">{item.code}</td>
                {rules.hasName && <td className="px-4">{item.name}</td>}
                {rules.hasDescription && <td className="px-4 text-text-secondary">{item.description || '–'}</td>}
                <td className="px-4">
                  {item.isActive ? <StatusChip tone="success" icon="check_circle">Đang dùng</StatusChip> : <StatusChip tone="neutral" icon="block">Ngừng dùng</StatusChip>}
                </td>
                <td className="px-4 text-right">
                  <Button variant="text" icon="edit" aria-label={`Sửa ${item.code}`} onClick={() => onEdit(item)}>
                    Sửa
                  </Button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  )
}
