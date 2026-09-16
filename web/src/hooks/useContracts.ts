import { useQuery } from '@tanstack/react-query'
import { getContracts } from '../api/contracts'

// Master data (migration 0002), not transaction data — no polling, no keepPreviousData.
// It practically never changes at runtime, so a long staleTime avoids refetching it on
// every filter change.
export function useContracts() {
  return useQuery({
    queryKey: ['contracts'],
    queryFn: getContracts,
    staleTime: 5 * 60 * 1000,
  })
}
