export type EffectiveStatus = 'Success' | 'Error' | 'Pending'

export interface PaymentListItem {
  id: number
  transactionId: string
  contractId: string | null
  amount: number | null
  paymentDate: string | null
  paymentStatus: string | null
  processingStatus: number
  effectiveStatus: EffectiveStatus
  attempts: number
  lastError: string | null
  validationError: string | null
  receivedAt: string
  processedAt: string | null
}

export interface PaymentDetail extends PaymentListItem {
  rawPayload: string
}

export interface PaymentsResponse {
  items: PaymentListItem[]
  page: number
  pageSize: number
  hasMore: boolean
}

export interface PaymentFilters {
  status?: EffectiveStatus
  contractId?: string
  page: number
  pageSize: number
}

export interface PaymentStats {
  total: number
  success: number
  error: number
  pending: number
}
