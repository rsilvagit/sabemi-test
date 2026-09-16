export type EffectiveStatus = 'Success' | 'Error' | 'Pending'

// Only meaningful when effectiveStatus is 'Error'. 'Validation' never actually reaches this
// list anymore — payloads that fail validation are excluded from /api/payments entirely
// (no dependable transaction/contract reference) and only show up via /api/payments/invalid
// — kept here so the type still matches what the API could theoretically return.
export type ErrorCategory = 'Validation' | 'PaymentFailure' | null

// Demo-only: mocked contract master data (Database/PostgreSQL/Migrations/Scripts/0002_*.sql),
// not something the webhook payload ever sends. Null when the contract has no seeded entry.
export type ContractType = 'Emprestimo' | 'Seguro' | null

export interface PaymentListItem {
  id: number
  transactionId: string
  contractId: string | null
  amount: number | null
  paymentDate: string | null
  paymentStatus: string | null
  processingStatus: number
  effectiveStatus: EffectiveStatus
  errorCategory: ErrorCategory
  contractType: ContractType
  installments: number | null
  totalValue: number | null
  installmentNumber: number | null
  attempts: number
  lastError: string | null
  validationError: string | null
  receivedAt: string
  processedAt: string | null
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
  contractType?: Exclude<ContractType, null>
  page: number
  pageSize: number
}

export interface PaymentStats {
  total: number
  success: number
  error: number
  pending: number
}

// A payload that failed validation — no dependable transaction/contract reference, so no
// effectiveStatus/errorCategory/contract enrichment, just the raw fields plus why it was
// rejected.
export interface InvalidPaymentEvent {
  id: number
  transactionId: string
  contractId: string | null
  amount: number | null
  paymentDate: string | null
  validationError: string | null
  receivedAt: string
}

export interface InvalidPaymentsResponse {
  items: InvalidPaymentEvent[]
  page: number
  pageSize: number
  hasMore: boolean
  total: number
}
