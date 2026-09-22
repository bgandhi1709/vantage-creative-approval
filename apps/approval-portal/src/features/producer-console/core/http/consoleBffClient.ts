import { CONSOLE_TOKEN_KEY } from '../state/consoleSessionSlice';

/**
 * The console's own HTTP client — deliberately not the portal's.
 *
 * Three things differ, and each is why this file exists rather than a flag on the shared client:
 *  - the credential is a bearer token, not a cookie;
 *  - a 401 does not navigate. The console is a nested app inside the same SPA, so it raises an
 *    event its own shell listens for and re-authenticates in place;
 *  - the error type is distinct, so a console error can never be handled by the client-facing
 *    portal's error boundary and vice versa.
 */

export const CONSOLE_SESSION_MISSING_EVENT = 'vantage-console-session-missing';

export class ConsoleHttpError extends Error {
  constructor(
    readonly status: number,
    message: string,
  ) {
    super(message);
    this.name = 'ConsoleHttpError';
  }
}

function readToken(): string | null {
  try {
    return window.sessionStorage.getItem(CONSOLE_TOKEN_KEY);
  } catch {
    return null;
  }
}

async function request<T>(method: string, path: string, body?: unknown): Promise<T> {
  const token = readToken();

  const init: RequestInit = {
    method,
    headers: {
      ...(token === null ? {} : { Authorization: `Bearer ${token}` }),
      ...(body === undefined ? {} : { 'Content-Type': 'application/json' }),
    },
  };

  if (body !== undefined) {
    init.body = JSON.stringify(body);
  }

  const response = await fetch(`/api${path}`, init);

  if (response.status === 401) {
    window.dispatchEvent(new CustomEvent(CONSOLE_SESSION_MISSING_EVENT));
    throw new ConsoleHttpError(401, 'The console session has expired.');
  }

  if (!response.ok) {
    throw new ConsoleHttpError(response.status, response.statusText);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  const text = await response.text();
  return (text.length === 0 ? undefined : JSON.parse(text)) as T;
}

export const consoleBffClient = {
  get: <T>(path: string) => request<T>('GET', path),
  post: <T>(path: string, body?: unknown) => request<T>('POST', path, body),
};
