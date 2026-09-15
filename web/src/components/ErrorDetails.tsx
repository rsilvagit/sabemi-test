import { usePaymentDetail } from '../hooks/usePaymentDetail'
import type { PaymentListItem } from '../api/types'

// Fetches and displays the raw payload plus the error text — this is where an operator
// investigating an incident actually clicks first.
export function ErrorDetails({ item }: { item: PaymentListItem }) {
  const { data, isLoading } = usePaymentDetail(item.id, true)

  return (
    <div className="space-y-2 border-t border-red-200 bg-red-50/50 px-4 py-3 text-sm">
      {item.validationError && (
        <p>
          <span className="font-medium text-red-800">Erro de validação: </span>
          <span className="text-gray-700">{item.validationError}</span>
        </p>
      )}
      {item.lastError && (
        <p>
          <span className="font-medium text-red-800">Última falha de processamento: </span>
          <span className="text-gray-700">{item.lastError}</span>
        </p>
      )}
      <p>
        <span className="font-medium text-red-800">Tentativas: </span>
        <span className="text-gray-700">{item.attempts}</span>
      </p>

      <div>
        <p className="mb-1 font-medium text-red-800">Payload recebido:</p>
        {isLoading ? (
          <p className="text-gray-500">Carregando…</p>
        ) : (
          <pre className="overflow-x-auto rounded bg-gray-900 p-3 text-xs text-gray-100">
            {data ? JSON.stringify(JSON.parse(data.rawPayload), null, 2) : '—'}
          </pre>
        )}
      </div>
    </div>
  )
}
