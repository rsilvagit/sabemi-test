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
