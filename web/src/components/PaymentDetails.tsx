import { formatContractType, formatCurrency, formatDate, formatInstallment } from '../lib/format'
import type { EffectiveStatus, PaymentListItem } from '../api/types'

// Shows the fields the API already parsed out of the payload, not the raw JSON — an
// operator wants "o que o banco mandou", not snake_case/aspas pra decifrar. Expandable for
// every status, not just errors — the raw data is just as useful to confirm a success.
const PANEL_STYLES: Record<EffectiveStatus, string> = {
  Success: 'border-green-200 bg-green-50/50',
  Error: 'border-red-200 bg-red-50/50',
  Pending: 'border-amber-200 bg-amber-50/50',
}

const LABEL_STYLES: Record<EffectiveStatus, string> = {
  Success: 'text-green-800',
  Error: 'text-red-800',
  Pending: 'text-amber-800',
}

export function PaymentDetails({ item }: { item: PaymentListItem }) {
  const panelClass = PANEL_STYLES[item.effectiveStatus]
  const labelClass = LABEL_STYLES[item.effectiveStatus]

  return (
    <div className={`space-y-3 border-t px-4 py-3 text-sm ${panelClass}`}>
      {item.validationError && (
        <p>
          <span className={`font-medium ${labelClass}`}>Erro de validação: </span>
          <span className="text-gray-700">{item.validationError}</span>
        </p>
      )}
      {item.lastError && (
        <p>
          <span className={`font-medium ${labelClass}`}>Última falha de processamento: </span>
          <span className="text-gray-700">{item.lastError}</span>
        </p>
      )}
      <p>
        <span className={`font-medium ${labelClass}`}>Tentativas: </span>
        <span className="text-gray-700">{item.attempts}</span>
      </p>

      <div>
        <p className={`mb-2 font-medium ${labelClass}`}>Dados recebidos do banco:</p>
        <dl className="grid grid-cols-2 gap-x-4 gap-y-2 sm:grid-cols-3">
          <Field
            label="Transação"
            value={item.transactionId.startsWith('MISSING:') ? 'Não informada' : item.transactionId}
          />
          <Field label="Contrato" value={item.contractId ?? 'Não informado'} />
          <Field label="Tipo de Contrato" value={formatContractType(item.contractType)} />
          <Field
            label="Parcela"
            value={formatInstallment(item.installments, item.installmentNumber)}
          />
          <Field label="Valor" value={formatCurrency(item.amount)} />
          <Field label="Valor total do contrato" value={formatCurrency(item.totalValue)} />
          <Field label="Data de pagamento" value={formatDate(item.paymentDate)} />
          <Field label="Recebido em" value={formatDate(item.receivedAt)} />
          <Field label="Processado em" value={formatDate(item.processedAt)} />
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
