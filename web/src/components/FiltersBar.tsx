import { useContracts } from '../hooks/useContracts'
import { Card } from './ui/Card'
import type { ContractType, EffectiveStatus } from '../api/types'

const STATUS_OPTIONS: { value: EffectiveStatus | undefined; label: string }[] = [
  { value: undefined, label: 'Todos' },
  { value: 'Success', label: 'Sucesso' },
  { value: 'Error', label: 'Erro' },
  { value: 'Pending', label: 'Pendente' },
]

const CONTRACT_TYPE_OPTIONS: { value: Exclude<ContractType, null> | undefined; label: string }[] = [
  { value: undefined, label: 'Todos os tipos' },
  { value: 'Emprestimo', label: 'Empréstimo' },
  { value: 'Seguro', label: 'Seguro' },
]

interface FiltersBarProps {
  status: EffectiveStatus | undefined
  contractId: string | undefined
  contractType: Exclude<ContractType, null> | undefined
  hasActiveFilters: boolean
  onStatusChange: (status: EffectiveStatus | undefined) => void
  onContractIdChange: (contractId: string | undefined) => void
  onContractTypeChange: (contractType: Exclude<ContractType, null> | undefined) => void
  onClear: () => void
}

export function FiltersBar({
  status,
  contractId,
  contractType,
  hasActiveFilters,
  onStatusChange,
  onContractIdChange,
  onContractTypeChange,
  onClear,
}: FiltersBarProps) {
  // Real contracts (migration 0002), not free text — avoids filtering by an ID that was
  // typed wrong or never existed.
  const { data: contracts } = useContracts()

  return (
    <Card className="flex flex-wrap items-center gap-3 p-4">
      <div className="flex gap-1">
        {STATUS_OPTIONS.map((option) => (
          <button
            key={option.label}
            type="button"
            onClick={() => onStatusChange(option.value)}
            className={`rounded-md px-3 py-1.5 text-sm font-medium transition-colors ${
              status === option.value
                ? 'bg-primary-600 text-white'
                : 'bg-gray-100 text-gray-700 hover:bg-gray-200'
            }`}
          >
            {option.label}
          </button>
        ))}
      </div>

      <select
        value={contractId ?? ''}
        onChange={(e) => onContractIdChange(e.target.value || undefined)}
        className="min-w-[180px] rounded-md border border-gray-300 py-1.5 px-3 text-sm focus:border-primary-500 focus:ring-1 focus:ring-primary-500 focus:outline-none"
      >
        <option value="">Todos os contratos</option>
        {contracts?.map((id) => (
          <option key={id} value={id}>
            {id}
          </option>
        ))}
      </select>

      <select
        value={contractType ?? ''}
        onChange={(e) => onContractTypeChange((e.target.value as Exclude<ContractType, null>) || undefined)}
        className="min-w-[160px] rounded-md border border-gray-300 py-1.5 px-3 text-sm focus:border-primary-500 focus:ring-1 focus:ring-primary-500 focus:outline-none"
      >
        {CONTRACT_TYPE_OPTIONS.map((option) => (
          <option key={option.label} value={option.value ?? ''}>
            {option.label}
          </option>
        ))}
      </select>

      {hasActiveFilters && (
        <button
          type="button"
          onClick={onClear}
          className="rounded-md px-3 py-1.5 text-sm font-medium text-gray-500 hover:text-gray-800"
        >
          Limpar filtros
        </button>
      )}
    </Card>
  )
}
