import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import type { ApiErrorPayload } from '../api/apiError'
import { ToastContext, type ToastContextValue } from '../context/ToastContext'
import Toast from './Toast'

const AUTO_DISMISS_MS = 6000
const MAX_VISIBLE_TOASTS = 3

interface ToastItem {
    id: number
    error: ApiErrorPayload
}

interface ToastProviderProps {
    children: ReactNode
}

function ToastProvider({ children }: ToastProviderProps) {
    const [toasts, setToasts] = useState<ToastItem[]>([])
    const nextIdRef = useRef(0)
    const timersRef = useRef(new Map<number, ReturnType<typeof setTimeout>>())

    const dismiss = useCallback((id: number) => {
        const timer = timersRef.current.get(id)
        if (timer) {
            clearTimeout(timer)
            timersRef.current.delete(id)
        }
        setToasts((current) => current.filter((toast) => toast.id !== id))
    }, [])

    const showError = useCallback(
        (error: ApiErrorPayload) => {
            nextIdRef.current += 1
            const id = nextIdRef.current
            setToasts((current) => [...current, { id, error }].slice(-MAX_VISIBLE_TOASTS))
            timersRef.current.set(
                id,
                setTimeout(()=> dismiss(id), AUTO_DISMISS_MS)
            )
        },
        [dismiss]
    )

    useEffect(() => {
        const timers = timersRef.current
        return () => {
            timers.forEach((timer) => clearTimeout(timer))
            timers.clear()
        }
    }, [])

    const value = useMemo<ToastContextValue>(() => ({ showError }), [showError])

    return (
        <ToastContext.Provider value={value}>
            {children}
            <div
                aria-label="Notifications"
                className="pointer-events-none fixed bottom-4 right-4 z-50 flex w-[calc(100%-2rem)] max-w-sm flex-col gap-2"
                data-testid="toast-region"
            >
                {toasts.map((toast) => (
                    <Toast key={toast.id} error={toast.error} onDismiss={() => dismiss(toast.id)} />
                ))}
            </div>
        </ToastContext.Provider>
    )
}

export default ToastProvider