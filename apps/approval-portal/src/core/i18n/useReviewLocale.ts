import { useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import { useParams } from 'react-router-dom';
import { DEFAULT_LOCALE, SUPPORTED_LOCALES, type SupportedLocale } from './index';

export function normalizeLocale(candidate: string | undefined): SupportedLocale {
  if (candidate === undefined) {
    return DEFAULT_LOCALE;
  }

  // Locale tags are case-insensitive on the wire ("de-de" in a hand-typed URL is still German),
  // but the resource bundles are keyed exactly, so normalize before matching.
  const match = SUPPORTED_LOCALES.find(
    (locale) => locale.toLowerCase() === candidate.toLowerCase(),
  );

  return match ?? DEFAULT_LOCALE;
}

/**
 * Resolves the locale from the route, not from the session and not from the browser.
 *
 * Review pages are reached from an emailed link by someone with no session at all, so the URL is
 * the only thing that knows which market the campaign runs in.
 */
export function useReviewLocale(): SupportedLocale {
  const { locale } = useParams<{ locale: string }>();
  const resolved = normalizeLocale(locale);
  const { i18n } = useTranslation();

  useEffect(() => {
    if (i18n.language !== resolved) {
      void i18n.changeLanguage(resolved);
    }
  }, [i18n, resolved]);

  return resolved;
}
