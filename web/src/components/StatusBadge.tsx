import type { EffectiveStatus } from '../api/types'

// Pill shape + 100/800 color pair, matching the jusnotify design system. Icon kept
// alongside color (not present in jusnotify) — color alone fails accessibility for
// colorblind users, and the PDF's "alerta visual claro" requirement calls for it.
const STYLES: Record<EffectiveStatus, { label: string; icon: string; className: string }> = {
  Success: { label: 'Sucesso', icon: '✓', className: 'bg-green-100 text-green-800' },
  Error: { label: 'Erro', icon: '⚠', className: 'bg-red-100 text-red-800' },
  Pending: { label: 'Pendente', icon: '⏱', className: 'bg-amber-100 text-amber-800' },
}

export function StatusBadge({ status }: { status: EffectiveStatus }) {
  const style = STYLES[status]

  return (
    <span
      className={`inline-flex items-center gap-1 rounded-full px-2.5 py-1 text-xs font-medium ${style.className}`}
    >
      <span aria-hidden="true">{style.icon}</span>
      {style.label}
    </span>
  )
}
