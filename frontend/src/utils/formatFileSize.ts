function formatFileSize(bytes: number): string {
  const mb = bytes / (1024 * 1024)
  if (mb < 1) {
    return `${Math.round(bytes / 1024)}KB`
  }
  return `${mb.toFixed(1)}MB`
}

export { formatFileSize }