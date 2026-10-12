import { render, screen } from '@testing-library/react'
import { describe, it, expect } from 'vitest'
import Header from '../../src/components/Header'

describe('Header', () => {
  it('renders the app brand title linking to home', () => {
    render(<Header />)
    const brandLink = screen.getByRole('link', { name: /dinospliceru home/i })
    expect(brandLink).toBeInTheDocument()
    expect(brandLink).toHaveAttribute('href', '/')
    expect(brandLink).toHaveTextContent('dinospliceru')
  })

  it('renders GitHub link with security attributes', () => {
    render(<Header />)
    const githubLink = screen.getByRole('link', { name: /github/i })
    expect(githubLink).toHaveAttribute('href', 'https://github.com/laeartes/dinospliceru')
    expect(githubLink).toHaveAttribute('target', '_blank')
    expect(githubLink).toHaveAttribute('rel', 'noopener noreferrer')
  })
})
