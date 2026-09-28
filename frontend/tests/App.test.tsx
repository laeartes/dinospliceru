import { render, screen } from '@testing-library/react'
import { describe, it, expect } from 'vitest'
import App from '../src/App'

describe('App', () => {
  it('renders header, upload video heading, and footer', () => {
    render(<App />)
    expect(screen.getByText('dinospliceru')).toBeInTheDocument()
    expect(screen.getByText(/upload video/i)).toBeInTheDocument()
    expect(screen.getByText('Tiesiog UAB')).toBeInTheDocument()
  })
})