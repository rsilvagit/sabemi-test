import { useQuery } from '@tanstack/react-query'
import { getPaymentById } from '../api/payments'

export function usePaymentDetail(id: number, enabled: boolean) {
  return useQuery({
    queryKey: ['payment', id],
    queryFn: () => getPaymentById(id),
    enabled,
  })
}
