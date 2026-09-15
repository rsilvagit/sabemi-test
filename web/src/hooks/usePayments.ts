import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { getPayments } from '../api/payments'
import type { PaymentFilters } from '../api/types'

export function usePayments(filters: PaymentFilters, autoRefresh: boolean) {
  return useQuery({
    queryKey: ['payments', filters],
    queryFn: () => getPayments(filters),
    // Without this the table blanks out on every refetch — the most visible defect a
    // polling dashboard can have.
    placeholderData: keepPreviousData,
    refetchInterval: autoRefresh ? 5000 : false,
    refetchOnWindowFocus: true,
  })
}
