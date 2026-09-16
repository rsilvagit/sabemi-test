const currencyFormatter = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' })
const dateFormatter = new Intl.DateTimeFormat('pt-BR', { dateStyle: 'short', timeStyle: 'medium' })

export function formatCurrency(value: number | null): string {
  if (value === null) return '—'
  return currencyFormatter.format(value)
}

export function formatDate(value: string | null): string {
  if (!value) return '—'
  return dateFormatter.format(new Date(value))
}

const CONTRACT_TYPE_LABELS: Record<string, string> = {
  Emprestimo: 'Empréstimo',
  Seguro: 'Seguro',
}

export function formatContractType(contractType: string | null): string {
  if (!contractType) return '—'
  return CONTRACT_TYPE_LABELS[contractType] ?? contractType
}

export function formatInstallment(installments: number | null, installmentNumber: number | null): string {
  if (!installments) return '—'
  return `${installmentNumber ?? '?'}/${installments}`
}
