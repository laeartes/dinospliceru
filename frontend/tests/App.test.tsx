import { render, screen } from '@testing-library/react'
import { describe, it, expect } from 'vitest'
import App from '../src/App'

describe('App', () => {
  it('renders the upload video heading', () => {
    render(<App />)
    expect(screen.getByText(/upload video/i)).toBeInTheDocument()
  })
})