import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { getInvalidPayments } from '../api/payments'

export function useInvalidPayments(page: number, pageSize: number, autoRefresh: boolean) {
  return useQuery({
    queryKey: ['invalid-payments', page, pageSize],
    queryFn: () => getInvalidPayments(page, pageSize),
    placeholderData: keepPreviousData,
    refetchInterval: autoRefresh ? 5000 : false,
  })
}
