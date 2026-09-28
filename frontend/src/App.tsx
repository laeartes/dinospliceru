import VideoUploader from './components/VideoUploader'

function App() {
  return (
    <main className="mx-auto max-w-xl px-4 py-12">
      <h1 className="mb-8 text-xl font-medium text-slate-800">
        Upload video
      </h1>
      <VideoUploader />
    </main>
  )
}

export default App