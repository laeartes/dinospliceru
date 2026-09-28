import { render, screen } from '@testing-library/react'
import { describe, it, expect } from 'vitest'
import Header from '../../src/components/Header'

describe('Header', () => {
  it('renders the app brand title', () => {
    render(<Header />)
    expect(screen.getByText('dinospliceru')).toBeInTheDocument()
  })

  it('renders GitHub link with security attributes', () => {
    render(<Header />)
    const githubLink = screen.getByRole('link', { name: /github/i })
    expect(githubLink).toHaveAttribute('href', 'https://github.com/laeartes/dinospliceru')
    expect(githubLink).toHaveAttribute('target', '_blank')
    expect(githubLink).toHaveAttribute('rel', 'noopener noreferrer')
  })
})
