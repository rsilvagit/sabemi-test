export class ApiError extends Error {
  status: number

  constructor(status: number, message: string) {
    super(message)
    this.name = 'ApiError'
    this.status = status
  }
}

// Relative paths only: in dev, Vite proxies /api and /webhooks to the backend (see
// vite.config.ts); in production, nginx does the same. This avoids CORS entirely and
// sidesteps the classic SPA-in-Docker trap where a build-time env var can't be swapped
// at container runtime.
export async function apiGet<T>(path: string): Promise<T> {
  const response = await fetch(path)

  if (!response.ok) {
    const problem = await response.json().catch(() => null)
    throw new ApiError(
      response.status,
      problem?.title ?? `Request failed with status ${response.status}`,
    )
  }

  return response.json() as Promise<T>
}
