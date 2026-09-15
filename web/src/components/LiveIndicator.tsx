interface LiveIndicatorProps {
  active: boolean
  onToggle: () => void
}

// A steadily pulsing dot reads as "live" more naturally than a ticking second counter —
// the exact age of the data matters less than whether it's still being refreshed.
export function LiveIndicator({ active, onToggle }: LiveIndicatorProps) {
  return (
    <button
      type="button"
      onClick={onToggle}
      title={active ? 'Clique para pausar a atualização automática' : 'Clique para retomar a atualização automática'}
      className="flex shrink-0 items-center gap-2 rounded-md px-2 py-1 text-sm whitespace-nowrap text-gray-500 hover:bg-gray-100"
    >
      <span className="relative flex h-2.5 w-2.5">
        {active && (
          <span className="absolute inline-flex h-full w-full animate-ping rounded-full bg-green-400 opacity-75" />
        )}
        <span
          className={`relative inline-flex h-2.5 w-2.5 rounded-full ${active ? 'bg-green-500' : 'bg-gray-400'}`}
        />
      </span>
      {active ? 'Atualizando automaticamente' : 'Atualização pausada'}
    </button>
  )
}
