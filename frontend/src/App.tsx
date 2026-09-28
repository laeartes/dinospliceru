import Header from './components/Header'
import Footer from './components/Footer'
import VideoUploader from './components/VideoUploader'

function App() {
  return (
    <div className="flex min-h-screen flex-col bg-cyber-bg text-cyber-dark">
      <Header />
      <main className="mx-auto flex w-full max-w-2xl flex-1 flex-col justify-center px-4 py-12">
        <VideoUploader />
      </main>
      <Footer />
    </div>
  )
}

export default App