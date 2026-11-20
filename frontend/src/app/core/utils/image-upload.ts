/** The largest picture the server accepts (700 KB); it stores every picture as WebP. */
export const MAX_IMAGE_BYTES = 700 * 1024

/** What the file picker offers. The server reads the bytes, so the browser's type is only a hint. */
export const IMAGE_TYPES = 'image/jpeg,image/png,image/webp'

/** Why a chosen file can't be uploaded, or null when it can. */
export function imageUploadError(file: File): string | null {
  if (!['image/jpeg', 'image/png', 'image/webp'].includes(file.type)) {
    return $localize`Choose a JPEG, PNG or WebP picture.`
  }
  if (file.size > MAX_IMAGE_BYTES) {
    return $localize`The picture is larger than 700 KB.`
  }
  return null
}
