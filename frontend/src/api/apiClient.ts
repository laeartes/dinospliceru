const DEFAULT_API_BASE_URL = 'http://localhost:5175'

// An empty VITE_API_BASE_URL is respected and means "same origin", for deployments that proxy /api
const API_BASE_URL = (import.meta.env.VITE_API_BASE_URL ?? DEFAULT_API_BASE_URL).replace(/\/+$/, '')

function apiUrl(path: string): string {
  return `${API_BASE_URL}${path}`
}

export { apiUrl }
