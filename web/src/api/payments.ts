import { apiGet } from './client'
import type { PaymentDetail, PaymentFilters, PaymentsResponse, PaymentStats } from './types'

export function getPayments(filters: PaymentFilters): Promise<PaymentsResponse> {
  const params = new URLSearchParams()
  if (filters.status) params.set('status', filters.status)
  if (filters.contractId) params.set('contractId', filters.contractId)
  params.set('page', String(filters.page))
  params.set('pageSize', String(filters.pageSize))

  return apiGet(`/api/payments?${params.toString()}`)
}

export function getPaymentById(id: number): Promise<PaymentDetail> {
  return apiGet(`/api/payments/${id}`)
}

export function getPaymentStats(): Promise<PaymentStats> {
  return apiGet('/api/payments/stats')
}
