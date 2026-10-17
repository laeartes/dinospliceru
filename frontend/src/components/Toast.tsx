import type { ApiErrorPayload } from '../api/apiError'

interface ToastProps {
    error: ApiErrorPayload
    onDismiss: () => void
}

function Toast({ error, onDismiss }: ToastProps) {
    return (
        <div
            role="alert"
            aria-live="assertive"
            className="pointer-events-auto flex w-full items-start gap-3 border-2 border-l-4 border-cyber-border border-l-cyber-pink bg-white p-4 text-left shadow-md"
            data-testid="toast"
         >
            <span className="font-mono text-sm text-cyber-pink" aria-hidden="true">
            (；´Д｀)
         </span>
         <div className="min-w-0 flex-1">
            <p className="text-sm text-cyber-dark break-words" data-testid="toast-message">
            {error.error}
            </p>
            <p className="mt-1 font-mono text-xs text-slate-400" data-testid="toast-code">
            {error.code}
            </p>
         </div>
         <button
            type="button"
            onClick={onDismiss}
            aria-label="Dismiss notification"
            className="font-mono text-xs text-slate-400 hover:text-cyber-dark focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-cyber-cyan"
         >
            ✕
         </button>
        </div>
    )
}

export default Toast