import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MemoryRouter, useLocation } from 'react-router-dom'
import type { ColumnDef } from '@tanstack/react-table'
import { describe, expect, it, vi } from 'vitest'
import { DataTable, type Paged, type TableQuery } from './DataTable'
import { EmptyState } from './States'

interface Student {
  id: string
  name: string
}

const columns: ColumnDef<Student, unknown>[] = [
  { accessorKey: 'name', header: 'Họ tên', meta: { sortKey: 'name' } },
]

function page(items: Student[], total = items.length): Paged<Student> {
  return { items, page: 1, pageSize: 20, totalCount: total, totalPages: Math.ceil(total / 20) || 1 }
}

function LocationProbe() {
  return <output data-testid="url">{useLocation().search}</output>
}

function renderTable(fetchPage: (q: TableQuery) => Promise<Paged<Student>>, initialUrl = '/') {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter initialEntries={[initialUrl]}>
        <DataTable
          queryKey={['students']}
          fetchPage={fetchPage}
          columns={columns}
          filters={[{ key: 'course', label: 'Môn', options: [{ value: 'EXE101', label: 'EXE101' }] }]}
          empty={<EmptyState title="Chưa có sinh viên" />}
        />
        <LocationProbe />
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

describe('DataTable', () => {
  it('reads paging, sort and filters from the URL and shows the range', async () => {
    const fetchPage = vi.fn(async () => page([{ id: '1', name: 'An' }], 45))
    renderTable(fetchPage, '/?page=2&sort=-name&course=EXE101&q=an')

    expect(await screen.findByText('An')).toBeInTheDocument()
    expect(fetchPage).toHaveBeenCalledWith({ page: 2, pageSize: 20, sort: '-name', search: 'an', filters: { course: 'EXE101' } })
    expect(screen.getByText('21–40 trên 45')).toBeInTheDocument()
    expect(screen.getByRole('columnheader', { name: /Họ tên/ })).toHaveAttribute('aria-sort', 'descending')
  })

  it('cycles sort on header click and resets to page 1', async () => {
    const fetchPage = vi.fn(async () => page([{ id: '1', name: 'An' }]))
    renderTable(fetchPage, '/?page=3')
    await screen.findByText('An')

    await userEvent.click(screen.getByRole('button', { name: /Họ tên/ }))

    await waitFor(() => expect(screen.getByTestId('url').textContent).toBe('?sort=name'))
  })

  it('shows the empty state without filters and "no results" with filters', async () => {
    const fetchPage = vi.fn(async () => page([]))
    const { unmount } = renderTable(fetchPage)
    expect(await screen.findByText('Chưa có sinh viên')).toBeInTheDocument()
    unmount()

    renderTable(fetchPage, '/?course=EXE101')
    expect(await screen.findByText('Không tìm thấy kết quả')).toBeInTheDocument()
    await userEvent.click(screen.getAllByRole('button', { name: 'Xóa bộ lọc' })[0])
    await waitFor(() => expect(screen.getByTestId('url').textContent).toBe(''))
  })

  it('shows an error banner with retry', async () => {
    const fetchPage = vi.fn().mockRejectedValueOnce(new Error('boom')).mockResolvedValueOnce(page([{ id: '1', name: 'Bình' }]))
    renderTable(fetchPage)

    expect(await screen.findByRole('alert')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: /Thử lại/ }))
    expect(await screen.findByText('Bình')).toBeInTheDocument()
  })
})
