import { useState, useRef, useCallback, type KeyboardEvent } from 'react'
import Header from './components/Header'
import Footer from './components/Footer'
import VideoUploader from './components/VideoUploader'
import SplicePanel from './components/SplicePanel'
import CutPanel from './components/CutPanel'
import CompressionPanel from './components/CompressionPanel'
import { useUploadConfig } from './hooks/useUploadConfig'
import type { VideoResponse } from './api/videoApi'

type WorkspaceView = 'overview' | 'cutter' | 'compressor' | 'splicer'

const WORKSPACE_TABS: ReadonlyArray<{
  id: WorkspaceView
  label: string
  symbol: string
  accentClass: string
}> = [
  { id: 'overview', label: 'Overview', symbol: '✦', accentClass: 'border-t-cyber-dark' },
  { id: 'cutter', label: 'Cut', symbol: '✧', accentClass: 'border-t-cyber-pink' },
  { id: 'compressor', label: 'Compress', symbol: '♡', accentClass: 'border-t-cyber-teal' },
  { id: 'splicer', label: 'Splice', symbol: '★', accentClass: 'border-t-cyber-cyan' },
]

function App() {
  const uploadConfig = useUploadConfig()
  const [readyVideo, setReadyVideo] = useState<VideoResponse | null>(null)
  const [selectedLocalFile, setSelectedLocalFile] = useState<File | null>(null)
  const [currentView, setCurrentView] = useState<WorkspaceView>('overview')
  const [isSplicing, setIsSplicing] = useState(false)
  const [isCutting, setIsCutting] = useState(false)
  const [isCompressing, setIsCompressing] = useState(false)

  const tabRefs = useRef<Record<WorkspaceView, HTMLButtonElement | null>>({
    overview: null,
    cutter: null,
    compressor: null,
    splicer: null,
  })
  const panelRefs = useRef<Record<WorkspaceView, HTMLDivElement | null>>({
    overview: null,
    cutter: null,
    compressor: null,
    splicer: null,
  })

  const handleVideoReady = useCallback((video: VideoResponse | null) => {
    setReadyVideo(video)
    if (!video) {
      setSelectedLocalFile(null)
      setCurrentView('overview')
      setIsSplicing(false)
      setIsCutting(false)
      setIsCompressing(false)
    }
  }, [])

  const navigateToView = useCallback((view: WorkspaceView, focusPanel: boolean) => {
    setCurrentView(view)
    if (focusPanel) {
      setTimeout(() => {
        panelRefs.current[view]?.focus()
      }, 0)
    }
  }, [])

  function isToolActive(view: WorkspaceView): boolean {
    if (view === 'cutter') return isCutting
    if (view === 'compressor') return isCompressing
    if (view === 'splicer') return isSplicing
    return false
  }

  function handleTabKeyDown(e: KeyboardEvent<HTMLButtonElement>, currentIndex: number) {
    let nextIndex: number | null = null
    if (e.key === 'ArrowRight') {
      nextIndex = (currentIndex + 1) % WORKSPACE_TABS.length
    } else if (e.key === 'ArrowLeft') {
      nextIndex = (currentIndex - 1 + WORKSPACE_TABS.length) % WORKSPACE_TABS.length
    } else if (e.key === 'Home') {
      nextIndex = 0
    } else if (e.key === 'End') {
      nextIndex = WORKSPACE_TABS.length - 1
    }

    if (nextIndex === null) return

    e.preventDefault()
    const nextView = WORKSPACE_TABS[nextIndex].id
    setCurrentView(nextView)
    tabRefs.current[nextView]?.focus()
  }

  return (
    <div className="flex min-h-screen flex-col bg-cyber-bg text-cyber-dark">
      <Header />
      <main className="mx-auto flex w-full max-w-2xl flex-1 flex-col justify-center px-4 py-12">
        {uploadConfig.status === 'ready' && (
          <>
            {readyVideo && (
              <div
                role="tablist"
                aria-label="Video workspace tools"
                className="-mb-[2px] relative z-10 grid grid-cols-4 border-2 border-b-0 border-cyber-border bg-slate-100 divide-x divide-cyber-border"
                data-testid="workspace-tablist"
              >
                {WORKSPACE_TABS.map((tab, index) => {
                  const isSelected = currentView === tab.id
                  const isActive = isToolActive(tab.id)
                  return (
                    <button
                      key={tab.id}
                      ref={(el) => {
                        tabRefs.current[tab.id] = el
                      }}
                      id={`workspace-tab-${tab.id}`}
                      type="button"
                      role="tab"
                      aria-selected={isSelected}
                      aria-controls={`workspace-panel-${tab.id}`}
                      aria-label={isActive ? `${tab.label} — processing in background` : tab.label}
                      tabIndex={isSelected ? 0 : -1}
                      title={isActive ? `${tab.label} — working hard~ ( •̀_•́ )` : undefined}
                      onClick={() => navigateToView(tab.id, false)}
                      onKeyDown={(e) => handleTabKeyDown(e, index)}
                      className={`inline-flex items-center justify-center gap-1.5 border-t-2 px-3 py-2.5 font-mono text-xs transition-colors cursor-pointer focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-cyber-cyan ${
                        isSelected
                          ? `${tab.accentClass} bg-white text-cyber-dark font-bold`
                          : 'border-t-transparent text-slate-500 hover:bg-white/70 hover:text-cyber-dark'
                      }`}
                      data-testid={`workspace-tab-${tab.id}`}
                    >
                      <span
                        className={isSelected ? 'text-cyber-pink' : 'text-slate-400'}
                        aria-hidden="true"
                      >
                        {tab.symbol}
                      </span>
                      <span>{tab.label}</span>
                      {isActive && (
                        <span
                          className="h-2 w-2 rounded-full bg-cyber-pink animate-pulse"
                          aria-hidden="true"
                          data-testid={`workspace-tab-${tab.id}-active`}
                        />
                      )}
                    </button>
                  )
                })}
              </div>
            )}

            <div
              ref={(el) => {
                panelRefs.current.overview = el
              }}
              id="workspace-panel-overview"
              role={readyVideo ? 'tabpanel' : undefined}
              aria-labelledby={readyVideo ? 'workspace-tab-overview' : undefined}
              tabIndex={readyVideo ? -1 : undefined}
              hidden={currentView !== 'overview'}
              className={currentView === 'overview' ? 'block focus:outline-none' : 'hidden'}
            >
              <VideoUploader
                config={uploadConfig.config}
                onFileSelect={setSelectedLocalFile}
                onVideoReady={handleVideoReady}
                onOpenSplicer={() => navigateToView('splicer', true)}
                isSplicing={isSplicing}
                onOpenCutter={() => navigateToView('cutter', true)}
                isCutting={isCutting}
                onOpenCompressor={() => navigateToView('compressor', true)}
                isCompressing={isCompressing}
              />
            </div>

            {readyVideo && (
              <>
                <div
                  ref={(el) => {
                    panelRefs.current.splicer = el
                  }}
                  id="workspace-panel-splicer"
                  role="tabpanel"
                  aria-labelledby="workspace-tab-splicer"
                  tabIndex={-1}
                  hidden={currentView !== 'splicer'}
                  className={currentView === 'splicer' ? 'block focus:outline-none' : 'hidden'}
                >
                  <SplicePanel
                    key={readyVideo.id}
                    video={readyVideo}
                    onBack={() => navigateToView('overview', true)}
                    onSplicingChange={setIsSplicing}
                  />
                </div>
                <div
                  ref={(el) => {
                    panelRefs.current.cutter = el
                  }}
                  id="workspace-panel-cutter"
                  role="tabpanel"
                  aria-labelledby="workspace-tab-cutter"
                  tabIndex={-1}
                  hidden={currentView !== 'cutter'}
                  className={currentView === 'cutter' ? 'block focus:outline-none' : 'hidden'}
                >
                  <CutPanel
                    key={readyVideo.id}
                    video={readyVideo}
                    localFile={selectedLocalFile}
                    onBack={() => navigateToView('overview', true)}
                    onCuttingChange={setIsCutting}
                  />
                </div>
                <div
                  ref={(el) => {
                    panelRefs.current.compressor = el
                  }}
                  id="workspace-panel-compressor"
                  role="tabpanel"
                  aria-labelledby="workspace-tab-compressor"
                  tabIndex={-1}
                  hidden={currentView !== 'compressor'}
                  className={currentView === 'compressor' ? 'block focus:outline-none' : 'hidden'}
                >
                  <CompressionPanel
                    key={readyVideo.id}
                    video={readyVideo}
                    onBack={() => navigateToView('overview', true)}
                    onCompressingChange={setIsCompressing}
                  />
                </div>
              </>
            )}
          </>
        )}

        {uploadConfig.status === 'loading' && (
          <div
            role="status"
            className="border-2 border-solid border-cyber-border bg-white p-8 text-center font-mono text-slate-500"
          >
            Loading! ( ๑&gt;ᴗ&lt;๑ )
          </div>
        )}

        {uploadConfig.status === 'error' && (
          <div className="flex flex-col items-center gap-3 border-2 border-solid border-cyber-border bg-white p-8 text-center">
            <p className="text-sm text-red-600 font-mono" role="alert">
              Couldn't load upload settings from the server.
            </p>
            <button
              type="button"
              onClick={uploadConfig.retry}
              className="bg-cyber-cyan px-4 py-2 font-medium text-cyber-dark hover:bg-cyber-cyan-hover hover:text-white focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-cyber-cyan focus-visible:ring-offset-2 transition-colors"
            >
              Retry ( -_ -)
            </button>
          </div>
        )}
      </main>
      <Footer />
    </div>
  )
}

export default App
