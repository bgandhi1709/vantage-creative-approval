import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import deDE from './locales/de-DE.json';
import enUS from './locales/en-US.json';

export const SUPPORTED_LOCALES = ['en-US', 'de-DE'] as const;
export type SupportedLocale = (typeof SUPPORTED_LOCALES)[number];
export const DEFAULT_LOCALE: SupportedLocale = 'en-US';

/**
 * No browser-language detection on purpose.
 *
 * A reviewer opens their link from an email, often on a device set to a different language than
 * the campaign runs in. The campaign's locale is in the URL, so that is what the page renders in;
 * see useReviewLocale.
 */
void i18n.use(initReactI18next).init({
  resources: {
    'en-US': { translation: enUS },
    'de-DE': { translation: deDE },
  },
  lng: DEFAULT_LOCALE,
  fallbackLng: DEFAULT_LOCALE,
  interpolation: { escapeValue: false },
});

export default i18n;
