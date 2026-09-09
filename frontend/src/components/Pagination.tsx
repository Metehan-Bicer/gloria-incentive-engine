import { useEffect, useMemo, useState } from 'react'

export const PAGE_SIZES = [10, 25, 50, 100]

export interface PaginationState<T> {
  page: number
  pages: number
  size: number
  total: number
  from: number
  to: number
  slice: T[]
  setPage: (page: number) => void
  setSize: (size: number) => void
}

export function usePagination<T>(items: T[], initialSize = 10): PaginationState<T> {
  const [page, setPage] = useState(1)
  const [size, setSize] = useState(initialSize)

  const total = items.length
  const pages = Math.max(1, Math.ceil(total / size))

  useEffect(() => {
    setPage(1)
  }, [items])

  useEffect(() => {
    if (page > pages) setPage(pages)
  }, [page, pages])

  const slice = useMemo(() => items.slice((page - 1) * size, page * size), [items, page, size])

  return {
    page,
    pages,
    size,
    total,
    from: total === 0 ? 0 : (page - 1) * size + 1,
    to: Math.min(page * size, total),
    slice,
    setPage,
    setSize: (next) => {
      setSize(next)
      setPage(1)
    },
  }
}

function pageNumbers(page: number, pages: number): (number | '…')[] {
  if (pages <= 7) return Array.from({ length: pages }, (_, i) => i + 1)
  const set = new Set<number>([1, pages, page - 1, page, page + 1])
  const list = [...set].filter((p) => p >= 1 && p <= pages).sort((a, b) => a - b)
  const result: (number | '…')[] = []
  list.forEach((p, i) => {
    if (i > 0 && p - list[i - 1] > 1) result.push('…')
    result.push(p)
  })
  return result
}

export function Pagination<T>({ state, label = 'kayıt' }: { state: PaginationState<T>; label?: string }) {
  if (state.total === 0) return null

  return (
    <div className="pagination">
      <div className="pagination-info">
        {state.from}–{state.to} / {state.total} {label}
      </div>
      <div className="pagination-controls">
        <label className="pagination-size">
          Sayfa başına
          <select value={state.size} onChange={(e) => state.setSize(Number(e.target.value))}>
            {PAGE_SIZES.map((s) => (
              <option key={s} value={s}>
                {s}
              </option>
            ))}
          </select>
        </label>
        <div className="pagination-pages">
          <button type="button" className="small" disabled={state.page === 1} onClick={() => state.setPage(state.page - 1)}>
            Önceki
          </button>
          {pageNumbers(state.page, state.pages).map((p, i) =>
            p === '…' ? (
              <span key={`gap-${i}`} className="pagination-gap">
                …
              </span>
            ) : (
              <button type="button" key={p} className={`small page-btn ${p === state.page ? 'active' : ''}`} onClick={() => state.setPage(p)}>
                {p}
              </button>
            ),
          )}
          <button type="button" className="small" disabled={state.page === state.pages} onClick={() => state.setPage(state.page + 1)}>
            Sonraki
          </button>
        </div>
      </div>
    </div>
  )
}
