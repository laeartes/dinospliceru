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
            <span className="text-cyber-pink" aria-hidden="true">★</span>
            <span className="text-xs font-mono text-slate-500">(◕‿◕✿)</span>
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
            className="group flex items-center gap-2 border border-cyber-pink bg-white px-3 py-1.5 text-xs font-medium text-cyber-pink focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-cyber-pink transition-colors"
          >
            <svg
              className="h-3.5 w-3.5 fill-transparent stroke-cyber-pink group-hover:fill-cyber-pink transition-colors"
              viewBox="0 0 24 24"
              strokeWidth={2}
              aria-hidden="true"
            >
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                d="M21 8.25c0-2.485-2.099-4.5-4.688-4.5-1.935 0-3.597 1.126-4.312 2.733-.715-1.607-2.377-2.733-4.313-2.733C5.1 3.75 3 5.765 3 8.25c0 7.22 9 12 9 12s9-4.78 9-12z"
              />
            </svg>
            <span>Support on Ko-fi</span>
          </a>
        </div>
      </div>
    </footer>
  )
}

export default Footer
