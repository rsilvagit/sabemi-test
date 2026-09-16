import { apiGet } from './client'
import type { InvalidPaymentsResponse, PaymentFilters, PaymentsResponse, PaymentStats } from './types'

export function getPayments(filters: PaymentFilters): Promise<PaymentsResponse> {
  const params = new URLSearchParams()
  if (filters.status) params.set('status', filters.status)
  if (filters.contractId) params.set('contractId', filters.contractId)
  if (filters.contractType) params.set('contractType', filters.contractType)
  params.set('page', String(filters.page))
  params.set('pageSize', String(filters.pageSize))

  return apiGet(`/api/payments?${params.toString()}`)
}

export function getPaymentStats(): Promise<PaymentStats> {
  return apiGet('/api/payments/stats')
}

export function getInvalidPayments(page: number, pageSize: number): Promise<InvalidPaymentsResponse> {
  const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
  return apiGet(`/api/payments/invalid?${params.toString()}`)
}
