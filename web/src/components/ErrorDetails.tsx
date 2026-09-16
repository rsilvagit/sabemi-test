import { formatCurrency, formatDate } from '../lib/format'
import type { PaymentListItem } from '../api/types'

// Shows the fields the API already parsed out of the payload, not the raw JSON — an
// operator wants "o que o banco mandou", not snake_case/aspas pra decifrar.
export function ErrorDetails({ item }: { item: PaymentListItem }) {
  return (
    <div className="space-y-3 border-t border-red-200 bg-red-50/50 px-4 py-3 text-sm">
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
        <p className="mb-2 font-medium text-red-800">Dados recebidos do banco:</p>
        <dl className="grid grid-cols-2 gap-x-4 gap-y-2 sm:grid-cols-3">
          <Field
            label="Transação"
            value={item.transactionId.startsWith('MISSING:') ? 'Não informada' : item.transactionId}
          />
          <Field label="Contrato" value={item.contractId ?? 'Não informado'} />
          <Field label="Valor" value={formatCurrency(item.amount)} />
          <Field label="Data de pagamento" value={formatDate(item.paymentDate)} />
          <Field label="Status (banco)" value={item.paymentStatus ?? 'Não informado'} />
        </dl>
      </div>
    </div>
  )
}

function Field({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <dt className="text-xs font-medium tracking-wide text-gray-500 uppercase">{label}</dt>
      <dd className="text-gray-800">{value}</dd>
    </div>
  )
}
