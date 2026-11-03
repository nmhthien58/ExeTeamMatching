import { useState } from 'react'
import { toApiError } from '@/shared/api/apiClient'
import { Banner, Button } from '@/shared/components/ui'
import { Drawer } from '@/features/workspace/components'
import { Field, inputClass } from '@/features/semesters/Field'
import { catalogKinds, useSaveCatalogItem, validateCatalog, type CatalogItem, type CatalogKind, type CatalogValues } from './api'

/** Thêm / sửa một mục danh mục (W12–W14). Ngừng dùng thay cho xóa: mục cũ vẫn gắn với dữ liệu đã có. */
export function CatalogDrawer({ kind, item, onClose, onSaved }: {
  kind: CatalogKind
  item: CatalogItem | null
  onClose: () => void
  onSaved: (message: string) => void
}) {
  const rules = catalogKinds[kind]
  const [values, setValues] = useState<CatalogValues>({
    code: item?.code ?? '',
    name: item?.name ?? '',
    description: item?.description ?? '',
    isActive: item?.isActive ?? true,
  })
  const [errors, setErrors] = useState<Record<string, string>>({})
  const save = useSaveCatalogItem(kind)
  const apiError = save.error ? toApiError(save.error) : null
  const fieldError = (key: string) => errors[key] ?? apiError?.fieldErrors[key]?.[0]
  const set = <K extends keyof CatalogValues>(key: K, value: CatalogValues[K]) => setValues((v) => ({ ...v, [key]: value }))

  const submit = () => {
    const found = validateCatalog(kind, values)
    setErrors(found)
    if (Object.keys(found).length > 0) return
    const code = values.code.trim().toUpperCase()
    save.mutate(
      {
        id: item?.id,
        input: {
          code,
          name: rules.hasName ? values.name.trim() : code,
          ...(rules.hasDescription ? { description: values.description.trim() || null } : {}),
          ...(item ? { isActive: values.isActive } : {}),
        },
      },
      { onSuccess: () => onSaved(item ? `Đã lưu ${rules.noun}` : `Đã thêm ${rules.noun}`) },
    )
  }

  return (
    <Drawer
      open
      onClose={onClose}
      title={item ? `Sửa ${rules.noun} ${item.code}` : `Thêm ${rules.noun}`}
      footer={
        <div className="flex justify-end gap-2">
          <Button variant="text" disabled={save.isPending} onClick={onClose}>Hủy</Button>
          <Button loading={save.isPending} onClick={submit}>Lưu</Button>
        </div>
      }
    >
      <div className="flex flex-col gap-4">
        {apiError && Object.keys(apiError.fieldErrors).length === 0 && <Banner tone="error" title={apiError.title}>{apiError.detail}</Banner>}
        <Field label={`Mã (${rules.codeHint})`} error={fieldError('code')}>
          <input value={values.code} maxLength={20} onChange={(e) => set('code', e.target.value.toUpperCase())} className={inputClass} />
        </Field>
        {rules.hasName && (
          <Field label="Tên" error={fieldError('name')}>
            <input value={values.name} maxLength={rules.maxName} onChange={(e) => set('name', e.target.value)} className={inputClass} />
          </Field>
        )}
        {rules.hasDescription && (
          <Field label="Lĩnh vực con, ngành chính (không bắt buộc)" error={fieldError('description')}>
            <textarea
              rows={4}
              maxLength={500}
              value={values.description}
              onChange={(e) => set('description', e.target.value)}
              className="rounded-sm border border-border-strong bg-surface px-3 py-2 text-body"
            />
          </Field>
        )}
        {item && (
          <label className="flex min-h-10 items-center justify-between gap-3 font-medium">
            Đang dùng
            <input type="checkbox" role="switch" className="size-5 accent-primary" checked={values.isActive} onChange={(e) => set('isActive', e.target.checked)} />
          </label>
        )}
        {item && !values.isActive && item.isActive && (
          <p className="text-body text-text-secondary">Ngừng dùng: không chọn được cho dữ liệu mới, dữ liệu đã gắn vẫn giữ nguyên.</p>
        )}
      </div>
    </Drawer>
  )
}
