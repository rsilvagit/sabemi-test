import { useState } from 'react'
import { FiltersBar } from '../components/FiltersBar'
import { LiveIndicator } from '../components/LiveIndicator'
import { Pagination } from '../components/Pagination'
import { PaymentsTable } from '../components/PaymentsTable'
import { StatsCards } from '../components/StatsCards'
import { useDashboardData } from '../hooks/useDashboardData'
import { usePaymentFilters } from '../hooks/usePaymentFilters'

export function Dashboard() {
  const { filters, setStatus, setContractId, setContractType, setPage, clear, hasActiveFilters } =
    usePaymentFilters()
  const [autoRefresh, setAutoRefresh] = useState(true)

  const { data, isLoading, isError } = useDashboardData(filters, autoRefresh)

  return (
    <div className="min-h-screen bg-[#eef2f7]">
      <header className="sticky top-0 z-40 border-b border-gray-200 bg-white">
        <div className="mx-auto max-w-6xl px-6 py-4">
          <div className="flex flex-wrap items-baseline justify-between gap-x-6 gap-y-1">
            <h1 className="min-w-0 text-xl font-bold text-gray-900">Sabemi — Pagamentos</h1>
            <LiveIndicator active={autoRefresh} onToggle={() => setAutoRefresh((v) => !v)} />
          </div>
          <p className="mt-1 max-w-2xl text-sm text-gray-500">Webhooks do banco parceiro</p>
        </div>
      </header>

      <main className="mx-auto max-w-6xl space-y-4 px-6 py-8">
        <StatsCards stats={data?.stats} activeStatus={filters.status} onSelect={setStatus} />

        <FiltersBar
          status={filters.status}
          contractId={filters.contractId}
          contractType={filters.contractType}
          hasActiveFilters={hasActiveFilters}
          onStatusChange={setStatus}
          onContractIdChange={setContractId}
          onContractTypeChange={setContractType}
          onClear={clear}
        />

        <PaymentsTable
          items={data?.payments.items ?? []}
          isLoading={isLoading}
          isError={isError}
        />

        {data && (
          <Pagination
            page={filters.page}
            hasMore={data.payments.hasMore}
            onPageChange={setPage}
          />
        )}
      </main>
    </div>
  )
}
