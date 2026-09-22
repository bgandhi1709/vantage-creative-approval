import { afterEach, describe, expect, it, vi } from 'vitest';
import { BffHttpError, bffClient } from './bffClient';

function mockFetch(response: Response) {
  const spy = vi.fn().mockResolvedValue(response);
  vi.stubGlobal('fetch', spy);
  return spy;
}

describe('bffClient', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
  });

  it('prefixes every path with /api and sends credentials', async () => {
    const fetchSpy = mockFetch(new Response(JSON.stringify({ ok: true }), { status: 200 }));

    await bffClient.get('/v1/northwind/reviews');

    expect(fetchSpy).toHaveBeenCalledWith(
      '/api/v1/northwind/reviews',
      expect.objectContaining({ credentials: 'include', method: 'GET' }),
    );
  });

  it('treats 204 as a successful void operation', async () => {
    mockFetch(new Response(null, { status: 204 }));

    await expect(bffClient.post('/auth/reviewer-session/logout')).resolves.toBeUndefined();
  });

  it('treats an empty 200 body as a successful void operation', async () => {
    // Calling response.json() here would throw and turn a success into a fake failure.
    mockFetch(new Response('', { status: 200 }));

    await expect(bffClient.post('/v1/northwind/reviews/x/submit')).resolves.toBeUndefined();
  });

  it('parses a JSON body when there is one', async () => {
    mockFetch(new Response(JSON.stringify({ reviewId: 'abc' }), { status: 200 }));

    await expect(bffClient.get<{ reviewId: string }>('/v1/northwind/reviews/abc')).resolves.toEqual(
      { reviewId: 'abc' },
    );
  });

  it('calls the unauthorized handler and throws on 401', async () => {
    mockFetch(new Response('', { status: 401 }));
    const onUnauthorized = vi.fn();

    await expect(bffClient.get('/v1/northwind/reviews', { onUnauthorized })).rejects.toThrow(
      BffHttpError,
    );
    expect(onUnauthorized).toHaveBeenCalledOnce();
  });

  it('throws a typed error carrying the status and the problem detail', async () => {
    mockFetch(
      new Response(JSON.stringify({ title: 'Review state conflict', detail: 'Already sent.' }), {
        status: 409,
      }),
    );

    await expect(bffClient.post('/v1/northwind/reviews/x/submit')).rejects.toMatchObject({
      name: 'BffHttpError',
      status: 409,
      message: 'Already sent.',
    });
  });

  it('falls back to the status text when the error body is not JSON', async () => {
    mockFetch(new Response('<html>gateway</html>', { status: 502, statusText: 'Bad Gateway' }));

    await expect(bffClient.get('/v1/northwind/reviews')).rejects.toMatchObject({
      status: 502,
      message: 'Bad Gateway',
    });
  });

  it('serializes a body and sets the content type only when there is one', async () => {
    const fetchSpy = mockFetch(new Response(null, { status: 204 }));

    await bffClient.post('/v1/northwind/reviews/x/decision', { outcome: 'Approved' });

    expect(fetchSpy).toHaveBeenCalledWith(
      '/api/v1/northwind/reviews/x/decision',
      expect.objectContaining({
        body: JSON.stringify({ outcome: 'Approved' }),
        headers: { 'Content-Type': 'application/json' },
      }),
    );
  });
});
