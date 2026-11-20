/** A text in the site's four languages; an empty one shows the site's built-in text. */
export interface ContentText {
  en: string | null
  it: string | null
  de: string | null
  fr: string | null
}

/** A picture uploaded in Admin → Website content, stored as WebP. */
export interface ContentImage {
  url: string
  width: number
  height: number
}

/** What was changed in the panel; keys missing here keep the site's own content. */
export interface SiteContent {
  texts: Record<string, ContentText>
  images: Record<string, ContentImage>
}
