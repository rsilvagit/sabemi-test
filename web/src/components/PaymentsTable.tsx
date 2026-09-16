import { Card } from './ui/Card'
import { PaymentRow } from './PaymentRow'
import type { PaymentListItem } from '../api/types'

interface PaymentsTableProps {
  items: PaymentListItem[]
  isLoading: boolean
  isError: boolean
}

export function PaymentsTable({ items, isLoading, isError }: PaymentsTableProps) {
  if (isError) {
    return (
      <div className="rounded-lg border border-red-200 bg-red-50 p-6 text-center text-red-700">
        Não foi possível carregar os pagamentos. Tentando novamente…
      </div>
    )
  }

  if (!isLoading && items.length === 0) {
    return (
      <Card className="p-8 text-center text-gray-500">
        Nenhum pagamento encontrado para os filtros aplicados.
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
            <th className="px-4 py-3" title="Dado de demonstração — não vem do webhook do banco">
              Tipo de Contrato
            </th>
            <th className="px-4 py-3">Valor</th>
            <th className="px-4 py-3">Status</th>
            <th className="px-4 py-3">Recebido em</th>
            <th className="px-4 py-3">Processado em</th>
          </tr>
        </thead>
        <tbody className="divide-y divide-gray-100">
          {items.map((item) => (
            <PaymentRow key={item.id} item={item} />
          ))}
        </tbody>
      </table>
    </Card>
  )
}
