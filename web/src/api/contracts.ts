import { apiGet } from './client'
import type { Contract } from './types'

export function getContracts(): Promise<Contract[]> {
  return apiGet('/api/payments/contracts')
}
