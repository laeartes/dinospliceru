import { useState } from 'react'
import VideoUploader from './components/VideoUploader'

function App() {
  const [selectedFile, setSelectedFile] = useState<File | null>(null)

  return (
    <main className="mx-auto max-w-xl px-4 py-12">
      <h1 className="mb-8 text-xl font-medium text-slate-800">
        Upload video
      </h1>
      <VideoUploader onFileSelect={setSelectedFile} />
      {selectedFile && (
        <p className="mt-4 text-sm text-slate-600">
          Selected: {selectedFile.name} ({Math.round(selectedFile.size / 1024 / 1024 * 10) / 10}MB)
        </p>
      )}
    </main>
  )
}

export default App