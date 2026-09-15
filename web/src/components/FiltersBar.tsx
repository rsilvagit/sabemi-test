import { Search } from 'lucide-react'
import { useEffect, useRef, useState } from 'react'
import { useDebouncedValue } from '../hooks/useDebouncedValue'
import { Card } from './ui/Card'
import type { EffectiveStatus } from '../api/types'

const STATUS_OPTIONS: { value: EffectiveStatus | undefined; label: string }[] = [
  { value: undefined, label: 'Todos' },
  { value: 'Success', label: 'Sucesso' },
  { value: 'Error', label: 'Erro' },
  { value: 'Pending', label: 'Pendente' },
]

interface FiltersBarProps {
  status: EffectiveStatus | undefined
  contractId: string | undefined
  hasActiveFilters: boolean
  onStatusChange: (status: EffectiveStatus | undefined) => void
  onContractIdChange: (contractId: string | undefined) => void
  onClear: () => void
}

export function FiltersBar({
  status,
  contractId,
  hasActiveFilters,
  onStatusChange,
  onContractIdChange,
  onClear,
}: FiltersBarProps) {
  // Local input state kept separate from the committed filter so typing feels instant;
  // only the debounced value triggers a refetch (300ms — otherwise every keystroke is a
  // request).
  const [contractInput, setContractInput] = useState(contractId ?? '')
  const debouncedContractId = useDebouncedValue(contractInput, 300)
  const isFirstRender = useRef(true)

  useEffect(() => {
    setContractInput(contractId ?? '')
  }, [contractId])

  useEffect(() => {
    if (isFirstRender.current) {
      isFirstRender.current = false
      return
    }
    onContractIdChange(debouncedContractId || undefined)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [debouncedContractId])

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

      <div className="relative min-w-[220px] flex-1">
        <Search
          className="pointer-events-none absolute top-1/2 left-2.5 h-4 w-4 -translate-y-1/2 text-gray-400"
          aria-hidden="true"
        />
        <input
          type="text"
          value={contractInput}
          onChange={(e) => setContractInput(e.target.value)}
          placeholder="Filtrar por ID do contrato..."
          className="w-full rounded-md border border-gray-300 py-1.5 pr-3 pl-9 text-sm placeholder:text-gray-400 focus:border-primary-500 focus:ring-1 focus:ring-primary-500 focus:outline-none"
        />
      </div>

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
