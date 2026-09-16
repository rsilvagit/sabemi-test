export class ApiError extends Error {
  status: number

  constructor(status: number, message: string) {
    super(message)
    this.name = 'ApiError'
    this.status = status
  }
}

// Empty by default: same-origin relative paths, proxied to the backend by Vite (dev) or
// nginx (docker-compose) — see vite.config.ts / nginx.conf.template. On Render this is set
// at build time to the API's public URL instead: nginx proxying cross-service on Render hit
// a TLS handshake failure against Render's own edge ("SSL alert handshake failure") that
// neither per-request DNS + SNI nor the private network (host not found in upstream) could
// get past, so the browser calls the API directly there — CORS instead of same-origin.
const API_ORIGIN = import.meta.env.VITE_API_ORIGIN ?? ''

// Baked in at `npm run build` (see web/Dockerfile ARG, .github/workflows/deploy.yml). Not a
// real secret — same reasoning as nginx injecting it server-side for the proxied case: it
// only gates casual scraping of payment data, not a security boundary.
const API_KEY = import.meta.env.VITE_API_KEY ?? ''

export async function apiGet<T>(path: string): Promise<T> {
  const response = await fetch(`${API_ORIGIN}${path}`, {
    headers: API_KEY ? { 'X-Api-Key': API_KEY } : {},
  })

  if (!response.ok) {
    const problem = await response.json().catch(() => null)
    throw new ApiError(
      response.status,
      problem?.title ?? `Request failed with status ${response.status}`,
    )
  }

  return response.json() as Promise<T>
}
