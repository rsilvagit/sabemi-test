import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { getPaymentStats, getPayments } from '../api/payments'
import type { PaymentFilters } from '../api/types'

// Payments and stats come from separate endpoints but must never drift on screen — a filter
// change or a poll tick fetches both together under one query, so React commits them to the
// screen in the same render instead of the stats card lagging behind the table by up to one
// refetchInterval.
export function useDashboardData(filters: PaymentFilters, autoRefresh: boolean) {
  return useQuery({
    queryKey: ['dashboard', filters],
    queryFn: async () => {
      const [payments, stats] = await Promise.all([getPayments(filters), getPaymentStats()])
      return { payments, stats }
    },
    placeholderData: keepPreviousData,
    refetchInterval: autoRefresh ? 5000 : false,
    refetchOnWindowFocus: true,
  })
}
