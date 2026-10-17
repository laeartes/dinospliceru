import { createContext } from 'react'
import type { ApiErrorPayload } from '../api/apiError'

interface ToastContextValue {
    showError: (error: ApiErrorPayload) => void
}

const ToastContext = createContext<ToastContextValue>({ showError: () => {} })

export { ToastContext }
export type { ToastContextValue }