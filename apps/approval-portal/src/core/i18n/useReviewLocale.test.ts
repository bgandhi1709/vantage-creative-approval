import { describe, expect, it } from 'vitest';
import { normalizeLocale } from './useReviewLocale';

describe('normalizeLocale', () => {
  it('accepts a supported locale from the route', () => {
    expect(normalizeLocale('de-DE')).toBe('de-DE');
  });

  it('ignores casing, because the tag may be hand-typed in a URL', () => {
    expect(normalizeLocale('de-de')).toBe('de-DE');
    expect(normalizeLocale('EN-us')).toBe('en-US');
  });

  it('falls back to the default for an unsupported locale', () => {
    // A campaign in a market the portal does not serve still has to render something.
    expect(normalizeLocale('fr-FR')).toBe('en-US');
  });

  it('falls back to the default when the route has no locale at all', () => {
    expect(normalizeLocale(undefined)).toBe('en-US');
  });
});
