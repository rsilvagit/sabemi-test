interface PaginationProps {
  page: number
  hasMore: boolean
  onPageChange: (page: number) => void
}

export function Pagination({ page, hasMore, onPageChange }: PaginationProps) {
  return (
    <div className="flex items-center justify-between px-1">
      <button
        type="button"
        onClick={() => onPageChange(page - 1)}
        disabled={page <= 1}
        className="rounded-md border border-gray-300 px-3 py-1.5 text-sm font-medium text-gray-700 transition-colors hover:bg-gray-50 disabled:opacity-40 disabled:hover:bg-transparent"
      >
        ← Anterior
      </button>
      <span className="text-sm text-gray-500">Página {page}</span>
      <button
        type="button"
        onClick={() => onPageChange(page + 1)}
        disabled={!hasMore}
        className="rounded-md border border-gray-300 px-3 py-1.5 text-sm font-medium text-gray-700 transition-colors hover:bg-gray-50 disabled:opacity-40 disabled:hover:bg-transparent"
      >
        Próxima →
      </button>
    </div>
  )
}
