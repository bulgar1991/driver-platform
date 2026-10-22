/** Route data for search engines and link previews. Routes without it are not indexed. */
export interface PageSeo {
  /** 120–160 characters: what the page offers, in the words people search with. */
  description: string
  /** Structured data to add (schema.org); only the home page describes the business. */
  schema?: 'business'
}
