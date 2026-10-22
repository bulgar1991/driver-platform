import { inject, LOCALE_ID, Pipe, PipeTransform } from '@angular/core'
import { siteLocale, SiteLocale } from '@/core/i18n/locales'
import { Translations } from '@/core/models'

/** A text typed in English in the admin panel, in that language; the English text when not translated. */
export function pickTranslation(
  english: string,
  translations: Translations | null | undefined,
  language: SiteLocale['code']
): string {
  const translated = language === 'en' ? null : translations?.[language]
  return translated?.trim() || english
}

/** `{{ point.name | translated: point.nameTranslations }}` in the site language. */
@Pipe({ name: 'translated' })
export class TranslatedPipe implements PipeTransform {
  private readonly language = siteLocale(inject(LOCALE_ID)).code

  transform(
    english: string | null | undefined,
    translations: Translations | null | undefined
  ): string {
    return english ? pickTranslation(english, translations, this.language) : ''
  }
}
