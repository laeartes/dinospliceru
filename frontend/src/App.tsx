import { useState } from 'react'
import Header from './components/Header'
import Footer from './components/Footer'
import VideoUploader from './components/VideoUploader'
import SplicePanel from './components/SplicePanel'
import { useUploadConfig } from './hooks/useUploadConfig'
import type { VideoResponse } from './api/videoApi'

function App() {
  const uploadConfig = useUploadConfig()
  const [readyVideo, setReadyVideo] = useState<VideoResponse | null>(null)
  const [currentView, setCurrentView] = useState<'overview' | 'splicer'>('overview')

  const handleVideoReady = (video: VideoResponse | null) => {
    setReadyVideo(video)
    if (!video) {
      setCurrentView('overview')
    }
  }

  return (
    <div className="flex min-h-screen flex-col bg-cyber-bg text-cyber-dark">
      <Header />
      <main className="mx-auto flex w-full max-w-2xl flex-1 flex-col justify-center px-4 py-12">
        {uploadConfig.status === 'ready' && (
          <>
            <div className={currentView === 'overview' ? 'block' : 'hidden'}>
              <VideoUploader
                config={uploadConfig.config}
                onVideoReady={handleVideoReady}
                onOpenSplicer={() => setCurrentView('splicer')}
              />
            </div>
            {readyVideo && (
              <div className={currentView === 'splicer' ? 'block' : 'hidden'}>
                <SplicePanel
                  video={readyVideo}
                  onBack={() => setCurrentView('overview')}
                />
              </div>
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
