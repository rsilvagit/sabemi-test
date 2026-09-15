import { useCallback, useEffect, useState } from 'react'
import type { EffectiveStatus, PaymentFilters } from '../api/types'

const DEFAULT_PAGE_SIZE = 25

// No React Router — it's a single screen. Filters live in the URL query string via the
// History API directly: shareable link, survives F5, back button works.
function readFromUrl(): PaymentFilters {
  const params = new URLSearchParams(window.location.search)
  return {
    status: (params.get('status') as EffectiveStatus | null) ?? undefined,
    contractId: params.get('contractId') ?? undefined,
    page: Number(params.get('page') ?? '1') || 1,
    pageSize: Number(params.get('pageSize') ?? String(DEFAULT_PAGE_SIZE)) || DEFAULT_PAGE_SIZE,
  }
}

export function usePaymentFilters() {
  const [filters, setFiltersState] = useState<PaymentFilters>(readFromUrl)

  useEffect(() => {
    const params = new URLSearchParams()
    if (filters.status) params.set('status', filters.status)
    if (filters.contractId) params.set('contractId', filters.contractId)
    params.set('page', String(filters.page))
    params.set('pageSize', String(filters.pageSize))
    const query = params.toString()
    const url = query ? `${window.location.pathname}?${query}` : window.location.pathname
    window.history.replaceState(null, '', url)
  }, [filters])

  // Any filter change resets to page 1 — otherwise the user filters and sees "no results"
  // because they're still on page 6 of a 1-page result.
  const setStatus = useCallback((status: EffectiveStatus | undefined) => {
    setFiltersState((f) => ({ ...f, status, page: 1 }))
  }, [])

  const setContractId = useCallback((contractId: string | undefined) => {
    setFiltersState((f) => ({ ...f, contractId, page: 1 }))
  }, [])

  const setPage = useCallback((page: number) => {
    setFiltersState((f) => ({ ...f, page }))
  }, [])

  const clear = useCallback(() => {
    setFiltersState({ page: 1, pageSize: DEFAULT_PAGE_SIZE })
  }, [])

  const hasActiveFilters = Boolean(filters.status || filters.contractId)

  return { filters, setStatus, setContractId, setPage, clear, hasActiveFilters }
}
