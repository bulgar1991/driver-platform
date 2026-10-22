/** A photo prepared by `npm run images`: WebP sizes and a tiny blurred preview. */
export interface ImageAsset {
  /** The largest size, for browsers that ignore srcset. */
  src: string
  srcset?: string
  width: number
  height: number
  /** Inline, a few hundred bytes: shown blurred until the image has loaded. */
  placeholder?: string
}
