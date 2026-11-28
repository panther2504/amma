/** Loads an image file and returns a JPEG resized so its longest side is at most `max` pixels. */
export async function resizeImage(file: File, max: number, quality: number): Promise<Blob> {
  const bitmap = await createImageBitmap(file, { imageOrientation: 'from-image' } as ImageBitmapOptions).catch(() => null)
  const img: CanvasImageSource & { width: number; height: number } = bitmap ?? (await loadImg(file))
  const scale = Math.min(1, max / Math.max(img.width, img.height))
  const w = Math.max(1, Math.round(img.width * scale)), h = Math.max(1, Math.round(img.height * scale))
  const canvas = document.createElement('canvas')
  canvas.width = w; canvas.height = h
  canvas.getContext('2d')!.drawImage(img, 0, 0, w, h)
  bitmap?.close()
  return new Promise((res, rej) => canvas.toBlob(b => (b ? res(b) : rej(new Error('Could not read this photo'))), 'image/jpeg', quality))
}

function loadImg(file: File) {
  return new Promise<HTMLImageElement>((res, rej) => {
    const url = URL.createObjectURL(file)
    const i = new Image()
    i.onload = () => { URL.revokeObjectURL(url); res(i) }
    i.onerror = () => { URL.revokeObjectURL(url); rej(new Error('Could not read this photo')) }
    i.src = url
  })
}

export const blobToDataUrl = (b: Blob) => new Promise<string>((res, rej) => { const r = new FileReader(); r.onload = () => res(r.result as string); r.onerror = rej; r.readAsDataURL(b) })
