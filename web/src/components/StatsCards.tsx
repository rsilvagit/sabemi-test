import type { EffectiveStatus, PaymentStats } from '../api/types'

interface StatsCardsProps {
  stats: PaymentStats | undefined
  activeStatus: EffectiveStatus | undefined
  onSelect: (status: EffectiveStatus | undefined) => void
}

const CARDS: {
  key: keyof PaymentStats
  label: string
  status: EffectiveStatus | undefined
  tint: string
}[] = [
  { key: 'total', label: 'Total', status: undefined, tint: 'bg-blue-50 text-blue-700' },
  { key: 'success', label: 'Sucesso', status: 'Success', tint: 'bg-green-50 text-green-700' },
  { key: 'error', label: 'Com erro', status: 'Error', tint: 'bg-red-50 text-red-700' },
  { key: 'pending', label: 'Pendentes', status: 'Pending', tint: 'bg-amber-50 text-amber-700' },
]

// Jusnotify-style summary cards: soft tinted background, big bold number, clickable to
// jump straight into the matching filter.
export function StatsCards({ stats, activeStatus, onSelect }: StatsCardsProps) {
  return (
    <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
      {CARDS.map((card) => (
        <button
          key={card.key}
          type="button"
          onClick={() => onSelect(activeStatus === card.status ? undefined : card.status)}
          className={`rounded-lg border p-4 text-left transition-shadow hover:shadow-md ${card.tint} ${
            activeStatus === card.status ? 'ring-2 ring-offset-1 ring-current' : 'border-transparent'
          }`}
        >
          <p className="text-sm font-medium opacity-80">{card.label}</p>
          <p className="text-3xl font-bold">{stats ? stats[card.key] : '—'}</p>
        </button>
      ))}
    </div>
  )
}
