import { IconName } from './icon.model'

/** A service offered on the public site. */
export interface ServiceOffering {
  title: string
  description: string
  icon: IconName
}
