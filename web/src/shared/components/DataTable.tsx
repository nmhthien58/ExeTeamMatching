import { useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import { useSearchParams } from 'react-router-dom'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { flexRender, getCoreRowModel, useReactTable, type ColumnDef } from '@tanstack/react-table'
import clsx from 'clsx'
import { Button, Icon } from './ui'
import { ErrorState, NoResults, TableSkeleton } from './States'

/** Kết quả phân trang chuẩn (docs/contracts/README.md). */
export interface Paged<T> {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}

/** Tham số gửi lên API: `page`, `pageSize`, `sort` (`name` / `-createdAt`), `search` và các bộ lọc. */
export interface TableQuery {
  page: number
  pageSize: number
  sort?: string
  search?: string
  filters: Record<string, string>
}

export interface TableFilter {
  key: string
  label: string
  options: { value: string; label: string }[]
}

declare module '@tanstack/react-table' {
  // eslint-disable-next-line @typescript-eslint/no-unused-vars
  interface ColumnMeta<TData, TValue> {
    /** Tên trường gửi ở `sort`; có thì cột bấm được để sắp xếp. */
    sortKey?: string
    align?: 'left' | 'right'
    /** Rộng tối thiểu (px) – cột chip trạng thái đủ chỗ cho chip dài nhất. */
    minWidth?: number
  }
}

const PAGE_SIZE = 20

/** Đọc / ghi tham số bảng trên URL để F5, chia sẻ link, Back đều giữ nguyên trạng thái (FE-07). */
export function useTableQuery(filterKeys: string[]) {
  const [params, setParams] = useSearchParams()
  const query: TableQuery = {
    page: Math.max(1, Number(params.get('page')) || 1),
    pageSize: PAGE_SIZE,
    sort: params.get('sort') ?? undefined,
    search: params.get('q') ?? undefined,
    filters: Object.fromEntries(filterKeys.flatMap((k) => (params.get(k) ? [[k, params.get(k)!]] : []))),
  }
  const update = (patch: Partial<Record<string, string | undefined>>, resetPage = true) => {
    const next = new URLSearchParams(params)
    for (const [k, v] of Object.entries(patch)) {
      if (v === undefined || v === '') next.delete(k)
      else next.set(k, v)
    }
    if (resetPage && !('page' in patch)) next.delete('page')
    setParams(next, { replace: true })
  }
  const clearFilters = () => update(Object.fromEntries([...filterKeys, 'q'].map((k) => [k, undefined])))
  return { query, update, clearFilters }
}

/**
 * Bảng dữ liệu dùng chung (FE-07, DESIGN.md 8.1): tìm kiếm (phím `/`), bộ lọc dạng dropdown kèm chip gỡ lọc,
 * sắp xếp theo cột, phân trang 20 dòng "1–20 trên 1.250", đủ trạng thái tải / trống / không kết quả / lỗi.
 * Dữ liệu lấy từ server (phân trang, lọc, sắp xếp đều ở API).
 */
export function DataTable<T>({
  queryKey,
  fetchPage,
  columns,
  filters = [],
  searchPlaceholder = 'Tìm kiếm',
  empty,
  toolbarRight,
  onRowClick,
  getRowId,
}: {
  queryKey: readonly unknown[]
  fetchPage: (query: TableQuery) => Promise<Paged<T>>
  columns: ColumnDef<T, unknown>[]
  filters?: TableFilter[]
  searchPlaceholder?: string
  /** Hiện khi bảng trống và không có bộ lọc nào. */
  empty: ReactNode
  toolbarRight?: ReactNode
  onRowClick?: (row: T) => void
  getRowId?: (row: T) => string
}) {
  const filterKeys = filters.map((f) => f.key)
  const { query, update, clearFilters } = useTableQuery(filterKeys)
  const hasFilters = !!query.search || Object.keys(query.filters).length > 0

  const result = useQuery({
    queryKey: [...queryKey, query],
    queryFn: () => fetchPage(query),
    placeholderData: keepPreviousData,
  })

  const data = useMemo(() => result.data?.items ?? [], [result.data])
  const table = useReactTable({
    data,
    columns,
    getCoreRowModel: getCoreRowModel(),
    manualPagination: true,
    manualSorting: true,
    getRowId,
  })

  const [searchText, setSearchText] = useState(query.search ?? '')
  const searchRef = useRef<HTMLInputElement>(null)
  useEffect(() => setSearchText(query.search ?? ''), [query.search])
  // Gõ xong 300 ms mới gọi API.
  useEffect(() => {
    if ((query.search ?? '') === searchText) return
    const t = setTimeout(() => update({ q: searchText.trim() || undefined }), 300)
    return () => clearTimeout(t)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [searchText])
  // Phím "/" đưa con trỏ vào ô tìm kiếm (DESIGN.md 8.2).
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      const target = e.target as HTMLElement
      if (e.key === '/' && !['INPUT', 'TEXTAREA', 'SELECT'].includes(target.tagName)) {
        e.preventDefault()
        searchRef.current?.focus()
      }
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [])

  const toggleSort = (key: string) => {
    const next = query.sort === key ? `-${key}` : query.sort === `-${key}` ? undefined : key
    update({ sort: next })
  }

  const total = result.data?.totalCount ?? 0
  const from = total === 0 ? 0 : (query.page - 1) * query.pageSize + 1
  const to = Math.min(query.page * query.pageSize, total)
  const totalPages = result.data?.totalPages ?? 1

  return (
    <section className="flex flex-col gap-3">
      <div className="flex flex-wrap items-center gap-2">
        <label className="relative w-full max-w-[320px]">
          <span className="sr-only">{searchPlaceholder}</span>
          <Icon name="search" size={20} className="pointer-events-none absolute left-3 top-2.5 text-text-secondary" />
          <input
            ref={searchRef}
            type="search"
            value={searchText}
            onChange={(e) => setSearchText(e.target.value)}
            placeholder={`${searchPlaceholder} (/)`}
            className="h-10 w-full rounded-sm border border-border-strong bg-surface pl-10 pr-3 text-body placeholder:text-text-secondary"
          />
        </label>
        {filters.map((f) => (
          <label key={f.key} className="flex items-center">
            <span className="sr-only">{f.label}</span>
            <select
              value={query.filters[f.key] ?? ''}
              onChange={(e) => update({ [f.key]: e.target.value || undefined })}
              className="h-10 rounded-sm border border-border-strong bg-surface px-3 text-body"
            >
              <option value="">{f.label}: Tất cả</option>
              {f.options.map((o) => (
                <option key={o.value} value={o.value}>
                  {o.label}
                </option>
              ))}
            </select>
          </label>
        ))}
        <div className="ml-auto flex gap-2">{toolbarRight}</div>
      </div>

      {hasFilters && (
        <div className="flex flex-wrap items-center gap-2" aria-label="Bộ lọc đang áp dụng">
          {query.search && <FilterChip label={`"${query.search}"`} onRemove={() => update({ q: undefined })} />}
          {filters
            .filter((f) => query.filters[f.key])
            .map((f) => (
              <FilterChip
                key={f.key}
                label={`${f.label}: ${f.options.find((o) => o.value === query.filters[f.key])?.label ?? query.filters[f.key]}`}
                onRemove={() => update({ [f.key]: undefined })}
              />
            ))}
          <Button variant="text" onClick={clearFilters}>
            Xóa bộ lọc
          </Button>
        </div>
      )}

      {result.isError && <ErrorState error={result.error} onRetry={() => result.refetch()} />}

      <div className="overflow-x-auto rounded-lg bg-surface shadow-elevation-1">
        {result.isLoading ? (
          <TableSkeleton columns={columns.length} />
        ) : data.length === 0 && !result.isError ? (
          hasFilters ? <NoResults onClear={clearFilters} /> : empty
        ) : (
          <table className="w-full border-collapse text-body" aria-busy={result.isFetching || undefined}>
            <thead className="sticky top-0 z-10 bg-background">
              {table.getHeaderGroups().map((hg) => (
                <tr key={hg.id}>
                  {hg.headers.map((h) => {
                    const meta = h.column.columnDef.meta
                    const sortKey = meta?.sortKey
                    const dir = query.sort === sortKey ? 'ascending' : query.sort === `-${sortKey}` ? 'descending' : undefined
                    return (
                      <th
                        key={h.id}
                        scope="col"
                        aria-sort={sortKey ? (dir ?? 'none') : undefined}
                        style={{ minWidth: meta?.minWidth }}
                        className={clsx('h-11 px-4 text-caption font-semibold text-text-secondary', meta?.align === 'right' ? 'text-right' : 'text-left')}
                      >
                        {sortKey ? (
                          <button type="button" onClick={() => toggleSort(sortKey)} className="inline-flex items-center gap-1 hover:text-text-primary">
                            {flexRender(h.column.columnDef.header, h.getContext())}
                            <Icon name={dir === 'descending' ? 'arrow_downward' : dir === 'ascending' ? 'arrow_upward' : 'unfold_more'} size={20} />
                          </button>
                        ) : (
                          flexRender(h.column.columnDef.header, h.getContext())
                        )}
                      </th>
                    )
                  })}
                </tr>
              ))}
            </thead>
            <tbody>
              {table.getRowModel().rows.map((row) => (
                <tr
                  key={row.id}
                  onClick={onRowClick ? () => onRowClick(row.original) : undefined}
                  className={clsx('h-[52px] border-b border-border last:border-b-0', onRowClick && 'cursor-pointer hover:bg-background')}
                >
                  {row.getVisibleCells().map((cell) => (
                    <td
                      key={cell.id}
                      className={clsx('px-4', cell.column.columnDef.meta?.align === 'right' && 'text-right tabular-nums')}
                    >
                      {flexRender(cell.column.columnDef.cell, cell.getContext())}
                    </td>
                  ))}
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {total > 0 && (
        <nav aria-label="Phân trang" className="flex items-center justify-end gap-2 text-body text-text-secondary">
          <span className="tabular-nums">
            {from}–{to} trên {total.toLocaleString('vi-VN')}
          </span>
          <Button variant="outline" aria-label="Trang trước" disabled={query.page <= 1} onClick={() => update({ page: String(query.page - 1) }, false)}>
            <Icon name="chevron_left" size={20} />
          </Button>
          <Button
            variant="outline"
            aria-label="Trang sau"
            disabled={query.page >= totalPages}
            onClick={() => update({ page: String(query.page + 1) }, false)}
          >
            <Icon name="chevron_right" size={20} />
          </Button>
        </nav>
      )}
    </section>
  )
}

function FilterChip({ label, onRemove }: { label: string; onRemove: () => void }) {
  return (
    <span className="inline-flex h-8 items-center gap-1 rounded-full bg-secondary-container pl-3 pr-1 text-body font-medium text-secondary-dark">
      {label}
      <button type="button" onClick={onRemove} aria-label={`Gỡ lọc ${label}`} className="grid size-6 place-items-center rounded-full hover:bg-white/60">
        <Icon name="close" size={20} />
      </button>
    </span>
  )
}
