import { useState } from 'react'
import { FiltersBar } from '../components/FiltersBar'
import { LiveIndicator } from '../components/LiveIndicator'
import { Pagination } from '../components/Pagination'
import { PaymentsTable } from '../components/PaymentsTable'
import { StatsCards } from '../components/StatsCards'
import { usePaymentFilters } from '../hooks/usePaymentFilters'
import { usePayments } from '../hooks/usePayments'
import { usePaymentStats } from '../hooks/usePaymentStats'

export function Dashboard() {
  const { filters, setStatus, setContractId, setPage, clear, hasActiveFilters } =
    usePaymentFilters()
  const [autoRefresh, setAutoRefresh] = useState(true)

  const { data, isLoading, isError } = usePayments(filters, autoRefresh)
  const { data: stats } = usePaymentStats(autoRefresh)

  return (
    <div className="min-h-screen bg-[#eef2f7]">
      <header className="sticky top-0 z-40 border-b border-gray-200 bg-white">
        <div className="mx-auto max-w-6xl px-6 py-4">
          <div className="flex flex-wrap items-baseline justify-between gap-x-6 gap-y-1">
            <h1 className="min-w-0 text-xl font-bold text-gray-900">Sabemi — Pagamentos</h1>
            <LiveIndicator active={autoRefresh} onToggle={() => setAutoRefresh((v) => !v)} />
          </div>
          <p className="mt-1 max-w-2xl text-sm text-gray-500">
            Notificações do banco parceiro. O processamento é assíncrono — um evento novo
            aparece como <span className="font-medium">Pendente</span> e muda sozinho em
            alguns segundos.
          </p>
        </div>
      </header>

      <main className="mx-auto max-w-6xl space-y-4 px-6 py-8">
        <StatsCards stats={stats} activeStatus={filters.status} onSelect={setStatus} />

        <FiltersBar
          status={filters.status}
          contractId={filters.contractId}
          hasActiveFilters={hasActiveFilters}
          onStatusChange={setStatus}
          onContractIdChange={setContractId}
          onClear={clear}
        />

        <PaymentsTable items={data?.items ?? []} isLoading={isLoading} isError={isError} />

        {data && (
          <Pagination page={filters.page} hasMore={data.hasMore} onPageChange={setPage} />
        )}
      </main>
    </div>
  )
}
