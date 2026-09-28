import { render, screen } from '@testing-library/react'
import { describe, it, expect } from 'vitest'
import Footer from '../../src/components/Footer'

describe('Footer', () => {
  it('renders team name Tiesiog UAB', () => {
    render(<Footer />)
    expect(screen.getByText('Tiesiog UAB')).toBeInTheDocument()
  })

  it('renders all team members with GitHub links', () => {
    render(<Footer />)
    const members = [
      { name: 'Mykolas', url: 'https://github.com/laeartes' },
      { name: 'Dinas', url: 'https://github.com/DinasZaranka' },
      { name: 'Žygimantas', url: 'https://github.com/raubatronas' },
      { name: 'Airidas', url: 'https://github.com/AiridasM' },
      { name: 'Emilijus', url: 'https://github.com/emilijus-trinkunas' },
    ]

    for (const member of members) {
      const link = screen.getByRole('link', { name: member.name })
      expect(link).toHaveAttribute('href', member.url)
      expect(link).toHaveAttribute('target', '_blank')
      expect(link).toHaveAttribute('rel', 'noopener noreferrer')
    }
  })

  it('renders Ko-fi support link', () => {
    render(<Footer />)
    const kofiLink = screen.getByRole('link', { name: /support on ko-fi/i })
    expect(kofiLink).toHaveAttribute('href', 'https://ko-fi.com/laeartes')
    expect(kofiLink).toHaveAttribute('target', '_blank')
    expect(kofiLink).toHaveAttribute('rel', 'noopener noreferrer')
  })
})
