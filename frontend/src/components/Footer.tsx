const TEAM_MEMBERS = [
  { name: 'Mykolas', url: 'https://github.com/laeartes' },
  { name: 'Dinas', url: 'https://github.com/DinasZaranka' },
  { name: 'Žygimantas', url: 'https://github.com/raubatronas' },
  { name: 'Airidas', url: 'https://github.com/AiridasM' },
  { name: 'Emilijus', url: 'https://github.com/emilijus-trinkunas' },
]

function Footer() {
  return (
    <footer className="w-full border-t border-cyber-border bg-white px-6 py-6 text-sm">
      <div className="mx-auto flex max-w-5xl flex-col items-center justify-between gap-4 md:flex-row">
        <div className="flex flex-col items-center gap-2 md:items-start">
          <div className="flex items-center gap-2">
            <span className="font-semibold text-cyber-dark">Tiesiog UAB</span>
            <span className="text-slate-400">&middot;</span>
            <span className="text-xs text-slate-500">team members</span>
          </div>

          <ul className="flex flex-wrap items-center justify-center gap-2 md:justify-start" aria-label="Team members">
            {TEAM_MEMBERS.map((member) => (
              <li key={member.name}>
                <a
                  href={member.url}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="border border-cyber-border px-2 py-0.5 text-xs text-slate-700 hover:border-cyber-cyan hover:text-cyber-cyan focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-cyber-cyan transition-colors"
                >
                  {member.name}
                </a>
              </li>
            ))}
          </ul>
        </div>

        <div className="flex items-center gap-4">
          <a
            href="https://ko-fi.com/laeartes"
            target="_blank"
            rel="noopener noreferrer"
            aria-label="Support on Ko-fi"
            className="flex items-center gap-2 border border-cyber-pink bg-white px-3 py-1.5 text-xs font-medium text-cyber-pink hover:bg-cyber-pink hover:text-white focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-cyber-pink transition-colors"
          >
            <span>Support on Ko-fi</span>
          </a>
        </div>
      </div>
    </footer>
  )
}

export default Footer
