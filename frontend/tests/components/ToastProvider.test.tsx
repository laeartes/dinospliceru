import { render, screen, fireEvent, act } from '@testing-library/react'
import { describe, it, expect, vi, afterEach } from 'vitest'
import ToastProvider from '../../src/components/ToastProvider'
import { useToast } from '../../src/hooks/useToast'
import type { ApiErrorPayload } from '../../src/api/apiError'

function ShowErrorButton({ error }: { error: ApiErrorPayload }) {
    const { showError } = useToast()
    return (
        <button type="button" onClick={() => showError(error)}>
            trigger    
        </button>
    )
}

function renderWithProvider(error : ApiErrorPayload) {
    render(
        <ToastProvider>
            <ShowErrorButton error={error} />
        </ToastProvider>
    )
}

describe('ToastProvider', () => {
    afterEach(() => {
        vi.useRealTimers()
    })

    it('shows a toast with the message and code when showError is called', () => {
        renderWithProvider({ error: 'Upload failed', code: 'INVALID_FILE_COUNT' })

        expect(screen.queryByTestId('toast')).not.toBeInTheDocument()

        fireEvent.click(screen.getByRole('button', { name: 'trigger' }))

        expect(screen.getByTestId('toast-message')).toHaveTextContent('Upload failed')
        expect(screen.getByTestId('toast-code')).toHaveTextContent('INVALID_FILE_COUNT')
    })

    it('removes the toast when it is dismissed', () => {
        renderWithProvider({ error: 'Upload failed', code: 'NETWORK_ERROR' })

        fireEvent.click(screen.getByRole('button', { name: 'trigger' }))
        fireEvent.click(screen.getByRole('button', { name: /dismiss notification/i }))
    
        expect(screen.queryByTestId('toast')).not.toBeInTheDocument()
    })

    it('removes the toast automatically after a few seconds', () => {
        vi.useFakeTimers()
        renderWithProvider({ error: 'Upload failed', code: 'NETWORK_ERROR' })

        fireEvent.click(screen.getByRole('button', { name: 'trigger' }))
        expect(screen.getByTestId('toast')).toBeInTheDocument()

        act(() => {
            vi.advanceTimersByTime(6000)
        })

        expect(screen.queryByTestId('toast')).not.toBeInTheDocument()
    })

    it('shows at most three toasts at once, keeping the newest', () => {
        renderWithProvider({ error: 'Upload failed', code: 'NETWORK_ERROR' })

        const trigger = screen.getByRole('button', { name: 'trigger' })
        for (let i = 0; i < 5; i++) {
            fireEvent.click(trigger)
        }

        expect(screen.getAllByTestId('toast')).toHaveLength(3)
    })
})