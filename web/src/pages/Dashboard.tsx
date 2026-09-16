import { useState } from 'react'
import { FiltersBar } from '../components/FiltersBar'
import { InvalidEventsTable } from '../components/InvalidEventsTable'
import { LiveIndicator } from '../components/LiveIndicator'
import { Pagination } from '../components/Pagination'
import { PaymentsTable } from '../components/PaymentsTable'
import { StatsCards } from '../components/StatsCards'
import { useDashboardData } from '../hooks/useDashboardData'
import { useInvalidPayments } from '../hooks/useInvalidPayments'
import { usePaymentFilters } from '../hooks/usePaymentFilters'

type View = 'payments' | 'invalid'

export function Dashboard() {
  const { filters, setStatus, setContractId, setContractType, setPage, clear, hasActiveFilters } =
    usePaymentFilters()
  const [autoRefresh, setAutoRefresh] = useState(true)
  const [view, setView] = useState<View>('payments')
  const [invalidPage, setInvalidPage] = useState(1)

  const { data, isLoading, isError } = useDashboardData(filters, autoRefresh)
  // Polls regardless of which tab is active — the count needs to stay fresh for the tab
  // label even while looking at the main payments list.
  const { data: invalidData, isLoading: invalidLoading, isError: invalidIsError } =
    useInvalidPayments(invalidPage, 25, autoRefresh)

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
        <div className="flex gap-1">
          <button
            type="button"
            onClick={() => setView('payments')}
            className={`rounded-md px-3 py-1.5 text-sm font-medium transition-colors ${
              view === 'payments'
                ? 'bg-primary-600 text-white'
                : 'bg-gray-100 text-gray-700 hover:bg-gray-200'
            }`}
          >
            Pagamentos
          </button>
          <button
            type="button"
            onClick={() => setView('invalid')}
            className={`rounded-md px-3 py-1.5 text-sm font-medium transition-colors ${
              view === 'invalid'
                ? 'bg-primary-600 text-white'
                : 'bg-gray-100 text-gray-700 hover:bg-gray-200'
            }`}
          >
            Eventos inválidos{invalidData ? ` (${invalidData.total})` : ''}
          </button>
        </div>

        {view === 'payments' ? (
          <>
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
          </>
        ) : (
          <>
            <p className="text-sm text-gray-500">
              Payloads que falharam na validação — sem <code>id_transacao</code> ou{' '}
              <code>id_contrato</code> confiáveis, por isso ficam fora da lista principal.
            </p>

            <InvalidEventsTable
              items={invalidData?.items ?? []}
              isLoading={invalidLoading}
              isError={invalidIsError}
            />

            {invalidData && (
              <Pagination
                page={invalidPage}
                hasMore={invalidData.hasMore}
                onPageChange={setInvalidPage}
              />
            )}
          </>
        )}
      </main>
    </div>
  )
}
