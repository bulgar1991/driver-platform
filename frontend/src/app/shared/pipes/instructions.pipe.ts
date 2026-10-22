import { inject, LOCALE_ID, Pipe, PipeTransform } from '@angular/core'
import { siteLocale } from '@/core/i18n/locales'
import { MeetingPoint } from '@/core/models'

/** A meeting point's instructions in the site language; the English text when not translated. */
@Pipe({ name: 'instructions' })
export class InstructionsPipe implements PipeTransform {
  private readonly language = siteLocale(inject(LOCALE_ID)).code

  transform(point: MeetingPoint | null | undefined): string | null {
    if (!point?.clientInstructions) {
      return null
    }
    const translated =
      this.language === 'en'
        ? null
        : point.clientInstructionsTranslations?.[this.language]
    return translated?.trim() || point.clientInstructions
  }
}
