function Header() {
  return (
    <header className="w-full border-b border-cyber-border bg-white px-6 py-4">
      <div className="mx-auto flex max-w-5xl items-center justify-between">
        <div className="flex items-center gap-3">
          <div className="h-6 w-2 bg-cyber-cyan" aria-hidden="true" />
          <span className="text-xl font-bold tracking-tight text-cyber-dark">
            dinospliceru
          </span>
        </div>

        <nav className="flex items-center gap-3" aria-label="External links">
          <a
            href="https://github.com/laeartes/dinospliceru"
            target="_blank"
            rel="noopener noreferrer"
            aria-label="GitHub repository"
            className="flex items-center gap-2 border border-cyber-border bg-white px-3 py-1.5 text-sm font-medium text-cyber-dark hover:border-cyber-cyan hover:text-cyber-cyan focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-cyber-cyan transition-colors"
          >
            <svg
              className="h-4 w-4 fill-current"
              viewBox="0 0 24 24"
              aria-hidden="true"
            >
              <path
                fillRule="evenodd"
                clipRule="evenodd"
                d="M12 2C6.477 2 2 6.484 2 12.017c0 4.425 2.865 8.18 6.839 9.504.5.092.682-.217.682-.483 0-.237-.008-.868-.013-1.703-2.782.605-3.369-1.343-3.369-1.343-.454-1.158-1.11-1.466-1.11-1.466-.908-.62.069-.608.069-.608 1.003.07 1.53 1.032 1.53 1.032.892 1.53 2.341 1.088 2.91.832.092-.647.35-1.088.636-1.338-2.22-.253-4.555-1.113-4.555-4.951 0-1.093.39-1.988 1.029-2.688-.103-.253-.446-1.272.098-2.65 0 0 .84-.27 2.75 1.026A9.564 9.564 0 0112 6.844c.85.004 1.705.115 2.504.337 1.909-1.296 2.747-1.027 2.747-1.027.546 1.379.202 2.398.1 2.651.64.7 1.028 1.595 1.028 2.688 0 3.848-2.339 4.695-4.566 4.943.359.309.678.92.678 1.855 0 1.338-.012 2.419-.012 2.747 0 .268.18.58.688.482A10.019 10.019 0 0022 12.017C22 6.484 17.522 2 12 2z"
              />
            </svg>
            <span>GitHub</span>
          </a>

          <a
            href="https://ko-fi.com/laeartes"
            target="_blank"
            rel="noopener noreferrer"
            aria-label="Support on Ko-fi"
            className="flex items-center gap-2 border border-cyber-pink bg-white px-3 py-1.5 text-sm font-medium text-cyber-pink hover:bg-cyber-pink hover:text-white focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-cyber-pink transition-colors"
          >
            <svg
              className="h-4 w-4 fill-current"
              viewBox="0 0 24 24"
              aria-hidden="true"
            >
              <path d="M23.881 8.948c-.773-4.085-4.859-4.593-4.859-4.593H.723c-.604 0-.679.798-.679.798s-.082 7.324-.022 11.822c.164 2.424 2.586 2.672 2.586 2.672s8.267-.023 11.966-.049c2.438-.426 2.683-2.566 2.658-3.734 4.352.24 7.422-2.831 6.649-6.916zm-11.022 5.04c-1.32 1.48-3.78 1.48-5.1 0-1.22-1.37-1.07-3.79.35-4.99 1.41-1.2 3.84-.25 4.4 1.34.56-1.59 2.99-2.54 4.4-1.34 1.42 1.2 1.57 3.62.35 4.99zm8.08-1.52c-.31 1.63-1.66 1.83-2.8 1.77V7.61c.96-.06 2.49-.07 2.8 1.77.16.92.16 2.17 0 3.09z" />
            </svg>
            <span>Ko-fi</span>
          </a>
        </nav>
      </div>
    </header>
  )
}

export default Header
