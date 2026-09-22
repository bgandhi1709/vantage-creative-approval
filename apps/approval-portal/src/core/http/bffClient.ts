/**
 * The only way the client half of the portal talks to the API.
 *
 * The SPA never calls the API directly: every request goes to /api on the same origin, which the
 * Vite proxy (dev) or nginx (containers) forwards. That keeps the session cookie first-party and
 * means the browser never needs to know the API's address.
 */

export class BffHttpError extends Error {
  constructor(
    readonly status: number,
    message: string,
  ) {
    super(message);
    this.name = 'BffHttpError';
  }
}

export interface BffClientOptions {
  /** Where to send an unauthenticated caller. Injected so tests do not navigate. */
  onUnauthorized?: () => void;
}

const defaultOptions: BffClientOptions = {
  onUnauthorized: () => {
    window.location.assign('/signed-out');
  },
};

async function request<T>(
  method: string,
  path: string,
  body?: unknown,
  options: BffClientOptions = defaultOptions,
): Promise<T> {
  const init: RequestInit = {
    method,
    // The reviewer's session is a cookie, so every call has to carry credentials.
    credentials: 'include',
  };

  if (body !== undefined) {
    init.headers = { 'Content-Type': 'application/json' };
    init.body = JSON.stringify(body);
  }

  const response = await fetch(`/api${path}`, init);

  if (response.status === 401) {
    options.onUnauthorized?.();
    throw new BffHttpError(401, 'The session has expired.');
  }

  if (!response.ok) {
    throw new BffHttpError(response.status, await readErrorMessage(response));
  }

  // A void operation may answer 204, or 200 with an empty body. Both are success, and calling
  // response.json() on either one throws — which is how an empty 200 turns into a fake failure.
  if (response.status === 204) {
    return undefined as T;
  }

  const text = await response.text();
  return (text.length === 0 ? undefined : JSON.parse(text)) as T;
}

async function readErrorMessage(response: Response): Promise<string> {
  try {
    const text = await response.text();

    if (text.length === 0) {
      return response.statusText;
    }

    const problem = JSON.parse(text) as { detail?: string; title?: string };
    return problem.detail ?? problem.title ?? response.statusText;
  } catch {
    return response.statusText;
  }
}

export const bffClient = {
  get: <T>(path: string, options?: BffClientOptions) =>
    request<T>('GET', path, undefined, options),
  post: <T>(path: string, body?: unknown, options?: BffClientOptions) =>
    request<T>('POST', path, body, options),
  put: <T>(path: string, body?: unknown, options?: BffClientOptions) =>
    request<T>('PUT', path, body, options),
  delete: <T>(path: string, options?: BffClientOptions) =>
    request<T>('DELETE', path, undefined, options),
};
