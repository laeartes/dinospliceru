import { render, screen } from '@testing-library/react'
import { describe, it, expect } from 'vitest'
import App from '../src/App'

describe('App', () => {
  it('renders header, video uploader dropzone, and footer', () => {
    render(<App />)
    expect(screen.getByText('dinospliceru')).toBeInTheDocument()
    expect(screen.getByRole('region', { name: /video uploader/i })).toBeInTheDocument()
    expect(screen.getByText('Tiesiog UAB')).toBeInTheDocument()
  })
})