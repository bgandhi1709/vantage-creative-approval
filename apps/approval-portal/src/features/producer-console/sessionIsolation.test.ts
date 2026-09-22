import { describe, expect, it } from 'vitest';
import { REVIEWER_SESSION_KEY } from '@/core/state/sessionSlice';
import {
  CONSOLE_SESSION_KEY,
  CONSOLE_TOKEN_KEY,
} from './core/state/consoleSessionSlice';

/**
 * One browser can hold a client reviewer session and a producer console session at the same time —
 * a producer opening a client's link is the normal case, not an edge case. These tests pin the
 * rule that keeps them apart, because the failure mode is silent: one session overwrites the other
 * and the symptom shows up later as an unexplained sign-out.
 */
describe('session storage isolation', () => {
  const consoleKeys = [CONSOLE_SESSION_KEY, CONSOLE_TOKEN_KEY];
  const portalKeys = [REVIEWER_SESSION_KEY];

  it('shares no key between the console and the client-facing portal', () => {
    for (const key of consoleKeys) {
      expect(portalKeys).not.toContain(key);
    }
  });

  it('keeps each side inside its own namespace', () => {
    for (const key of consoleKeys) {
      expect(key.startsWith('vantage.console.')).toBe(true);
    }

    for (const key of portalKeys) {
      expect(key.startsWith('vantage.reviewer.')).toBe(true);
    }
  });

  it('lets both sessions exist side by side without clobbering each other', () => {
    window.sessionStorage.setItem(REVIEWER_SESSION_KEY, JSON.stringify({ clientCode: 'northwind' }));
    window.sessionStorage.setItem(CONSOLE_TOKEN_KEY, 'producer-token');

    expect(window.sessionStorage.getItem(REVIEWER_SESSION_KEY)).toContain('northwind');
    expect(window.sessionStorage.getItem(CONSOLE_TOKEN_KEY)).toBe('producer-token');
  });
});
