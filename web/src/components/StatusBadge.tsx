import type { EffectiveStatus, ErrorCategory } from '../api/types'

// Pill shape + 100/800 color pair, matching the jusnotify design system. Icon kept
// alongside color (not present in jusnotify) — color alone fails accessibility for
// colorblind users, and the PDF's "alerta visual claro" requirement calls for it.
const STYLES: Record<EffectiveStatus, { label: string; icon: string; className: string }> = {
  Success: { label: 'Sucesso', icon: '✓', className: 'bg-green-100 text-green-800' },
  Error: { label: 'Erro', icon: '⚠', className: 'bg-red-100 text-red-800' },
  Pending: { label: 'Pendente', icon: '⏱', className: 'bg-amber-100 text-amber-800' },
}

// A malformed payload (never reached a payment outcome) and a payload the bank fully
// processed and rejected are both "Erro", but reading the dashboard they mean different
// things — a validation error is an integration/data problem, a payment failure is business
// as usual. Same red pill (still one category for the filter), different label.
const ERROR_LABELS: Record<'Validation' | 'PaymentFailure', string> = {
  Validation: 'Erro de validação',
  PaymentFailure: 'Falha de pagamento',
}

export function StatusBadge({
  status,
  errorCategory,
}: {
  status: EffectiveStatus
  errorCategory?: ErrorCategory
}) {
  const style = STYLES[status]
  const label = status === 'Error' && errorCategory ? ERROR_LABELS[errorCategory] : style.label

  return (
    <span
      className={`inline-flex items-center gap-1 rounded-full px-2.5 py-1 text-xs font-medium ${style.className}`}
    >
      <span aria-hidden="true">{style.icon}</span>
      {label}
    </span>
  )
}
