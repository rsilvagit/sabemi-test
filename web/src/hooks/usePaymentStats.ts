import { useQuery } from '@tanstack/react-query'
import { getPaymentStats } from '../api/payments'

export function usePaymentStats(autoRefresh: boolean) {
  return useQuery({
    queryKey: ['payment-stats'],
    queryFn: getPaymentStats,
    refetchInterval: autoRefresh ? 5000 : false,
  })
}
