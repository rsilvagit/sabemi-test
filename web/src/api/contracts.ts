import { apiGet } from './client'

export function getContractIds(): Promise<string[]> {
  return apiGet('/api/payments/contracts')
}
