import { createSlice, type PayloadAction } from '@reduxjs/toolkit';

/**
 * Console storage keys.
 *
 * Both are namespaced under "vantage.console." and must never collide with the client-facing
 * keys under "vantage.reviewer.". One browser can hold both sessions at once — a producer opening
 * a client's review link is the normal case — and a shared key would let one session silently
 * overwrite the other. A test asserts the two namespaces stay disjoint.
 */
export const CONSOLE_TOKEN_KEY = 'vantage.console.token';
export const CONSOLE_SESSION_KEY = 'vantage.console.session';

export interface ConsoleSessionState {
  email: string | null;
  clientCode: string | null;
}

function loadState(): ConsoleSessionState {
  const empty: ConsoleSessionState = { email: null, clientCode: null };

  try {
    const stored = window.sessionStorage.getItem(CONSOLE_SESSION_KEY);
    return stored === null ? empty : ({ ...empty, ...JSON.parse(stored) } as ConsoleSessionState);
  } catch {
    return empty;
  }
}

function persist(state: ConsoleSessionState): void {
  try {
    window.sessionStorage.setItem(CONSOLE_SESSION_KEY, JSON.stringify(state));
  } catch {
    // Session storage is a convenience here, never a requirement.
  }
}

export const consoleSessionSlice = createSlice({
  name: 'consoleSession',
  initialState: loadState(),
  reducers: {
    signedIn(state, action: PayloadAction<{ email: string; token: string }>) {
      state.email = action.payload.email;

      try {
        window.sessionStorage.setItem(CONSOLE_TOKEN_KEY, action.payload.token);
      } catch {
        // The token still lives in memory for this tab.
      }

      persist(state);
    },
    clientSelected(state, action: PayloadAction<string>) {
      state.clientCode = action.payload;
      persist(state);
    },
    signedOut(state) {
      state.email = null;
      state.clientCode = null;

      try {
        window.sessionStorage.removeItem(CONSOLE_TOKEN_KEY);
        window.sessionStorage.removeItem(CONSOLE_SESSION_KEY);
      } catch {
        // Nothing to clean up if storage is unavailable.
      }

      persist(state);
    },
  },
});

export const { signedIn, clientSelected, signedOut } = consoleSessionSlice.actions;
