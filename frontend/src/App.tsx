import Header from './components/Header'
import Footer from './components/Footer'
import VideoUploader from './components/VideoUploader'

function App() {
  return (
    <div className="flex min-h-screen flex-col bg-cyber-bg text-cyber-dark">
      <Header />
      <main className="mx-auto flex w-full max-w-2xl flex-1 flex-col justify-center px-4 py-12">
        <div className="mb-6 flex items-center gap-3">
          <div className="h-5 w-1.5 bg-cyber-cyan" aria-hidden="true" />
          <h1 className="text-xl font-semibold tracking-tight text-cyber-dark">
            Upload video
          </h1>
        </div>
        <VideoUploader />
      </main>
      <Footer />
    </div>
  )
}

export default App