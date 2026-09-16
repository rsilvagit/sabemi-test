import { useState } from 'react'
import { StatusBadge } from './StatusBadge'
import { ErrorDetails } from './ErrorDetails'
import { formatContractType, formatCurrency, formatDate } from '../lib/format'
import type { PaymentListItem } from '../api/types'

export function PaymentRow({ item }: { item: PaymentListItem }) {
  const [expanded, setExpanded] = useState(false)
  const isError = item.effectiveStatus === 'Error'

  return (
    <>
      <tr
        onClick={() => setExpanded((v) => !v)}
        className={`cursor-pointer text-sm ${
          isError ? 'border-l-4 border-l-red-500 bg-red-50' : 'hover:bg-gray-50'
        }`}
      >
        <td className="px-4 py-3 font-mono text-xs text-gray-600">
          {item.transactionId.startsWith('MISSING:') ? 'Não informada' : item.transactionId}
        </td>
        <td className="px-4 py-3 text-primary-700">{item.contractId ?? '—'}</td>
        <td className="px-4 py-3 text-gray-600">
          {formatContractType(item.contractType, item.installments)}
        </td>
        <td className="px-4 py-3">{formatCurrency(item.amount)}</td>
        <td className="px-4 py-3">
          <StatusBadge status={item.effectiveStatus} errorCategory={item.errorCategory} />
        </td>
        <td className="px-4 py-3 text-gray-500">{formatDate(item.receivedAt)}</td>
        <td className="px-4 py-3 text-gray-500">{formatDate(item.processedAt)}</td>
      </tr>
      {expanded && isError && (
        <tr>
          <td colSpan={7} className="p-0">
            <ErrorDetails item={item} />
          </td>
        </tr>
      )}
    </>
  )
}
