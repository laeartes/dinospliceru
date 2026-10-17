import { render, screen, fireEvent } from '@testing-library/react'
import { describe, it, expect, vi } from 'vitest'
import Toast from '../../src/components/Toast'

describe('Toast', () => {
    it('renders the error message from the payload as an alert', () => {
        render(<Toast error={{ error: 'No video with that id exists.', code: 'VIDEO_NOT_FOUND' }} onDismiss={vi.fn()} />)

        expect(screen.getByRole('alert')).toBeInTheDocument()
        expect(screen.getByTestId('toast-message')).toHaveTextContent('No video with that id exists.')
    })

    it('shows the error code from the payload', () => {
        render(<Toast error={{ error: 'File too large', code: 'FILE_TOO_LARGE' }} onDismiss={vi.fn()} />)

        expect(screen.getByTestId('toast-code')).toHaveTextContent('FILE_TOO_LARGE')
    })

    it('calls onDismiss when the dismiss button is clicked', () => {
        const onDismiss = vi.fn()
        render(<Toast error={{ error: 'Something went wrong', code: 'UNKNOWN_ERROR' }} onDismiss={onDismiss} />)

        fireEvent.click(screen.getByRole('button', { name: /dismiss notification/i }))

        expect(onDismiss).toHaveBeenCalledTimes(1)
    })
})