import { Card } from './ui/Card'
import { formatCurrency, formatDate } from '../lib/format'
import type { InvalidPaymentEvent } from '../api/types'

interface InvalidEventsTableProps {
  items: InvalidPaymentEvent[]
  isLoading: boolean
  isError: boolean
}

// No contract/type/installment columns here — a payload that failed validation has no
// dependable transaction/contract reference, so there's nothing trustworthy to enrich with
// a contract join. Every row is red: they're all rejected by definition, no status column.
export function InvalidEventsTable({ items, isLoading, isError }: InvalidEventsTableProps) {
  if (isError) {
    return (
      <div className="rounded-lg border border-red-200 bg-red-50 p-6 text-center text-red-700">
        Não foi possível carregar os eventos inválidos. Tentando novamente…
      </div>
    )
  }

  if (!isLoading && items.length === 0) {
    return (
      <Card className="p-8 text-center text-gray-500">
        Nenhum evento inválido registrado.
      </Card>
    )
  }

  return (
    <Card className="overflow-x-auto">
      <table className="w-full text-left text-sm">
        <thead>
          <tr className="border-b border-gray-200 bg-gray-50 text-xs font-semibold text-gray-500 uppercase">
            <th className="px-4 py-3">Transação</th>
            <th className="px-4 py-3">Contrato</th>
            <th className="px-4 py-3">Valor</th>
            <th className="px-4 py-3">Erro de validação</th>
            <th className="px-4 py-3">Recebido em</th>
          </tr>
        </thead>
        <tbody className="divide-y divide-gray-100">
          {items.map((item) => (
            <tr key={item.id} className="border-l-4 border-l-red-500 bg-red-50 text-sm">
              <td className="px-4 py-3 font-mono text-xs text-gray-600">
                {item.transactionId.startsWith('MISSING:') ? 'Não informada' : item.transactionId}
              </td>
              <td className="px-4 py-3 text-gray-600">{item.contractId ?? 'Não informado'}</td>
              <td className="px-4 py-3">{formatCurrency(item.amount)}</td>
              <td className="px-4 py-3 text-red-800">{item.validationError ?? '—'}</td>
              <td className="px-4 py-3 text-gray-500">{formatDate(item.receivedAt)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </Card>
  )
}
