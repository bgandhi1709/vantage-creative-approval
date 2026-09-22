import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { consoleApi } from '../core/api/consoleApi';
import { CONSOLE_SESSION_MISSING_EVENT } from '../core/http/consoleBffClient';
import {
  clientSelected,
  signedIn,
  signedOut,
} from '../core/state/consoleSessionSlice';
import { useConsoleDispatch, useConsoleSelector } from '../core/state/consoleStore';

/** The internal work list: what is out with each client, and what is still a draft. */
export function ConsolePage() {
  const dispatch = useConsoleDispatch();
  const email = useConsoleSelector((state) => state.session.email);
  const clientCode = useConsoleSelector((state) => state.session.clientCode) ?? 'northwind';
  const queryClient = useQueryClient();
  const [emailDraft, setEmailDraft] = useState('producer@vantage.test');

  // The console re-authenticates in place instead of navigating away: it is a nested app, and a
  // redirect would throw away whatever the producer was in the middle of.
  useEffect(() => {
    const handler = () => dispatch(signedOut());
    window.addEventListener(CONSOLE_SESSION_MISSING_EVENT, handler);
    return () => window.removeEventListener(CONSOLE_SESSION_MISSING_EVENT, handler);
  }, [dispatch]);

  const signIn = useMutation({
    mutationFn: (address: string) => consoleApi.signIn(address),
    onSuccess: (result, address) => {
      dispatch(signedIn({ email: address, token: result.accessToken }));
    },
  });

  const outstanding = useQuery({
    queryKey: ['console', 'outstanding', clientCode],
    queryFn: () => consoleApi.getOutstanding(clientCode),
    enabled: email !== null,
  });

  const submit = useMutation({
    mutationFn: (reviewId: string) => consoleApi.submit(clientCode, reviewId),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['console', 'outstanding', clientCode] });
    },
  });

  if (email === null) {
    return (
      <section className="console console--signin">
        <h1>Producer console</h1>
        <label htmlFor="console-email">Work email</label>
        <input
          id="console-email"
          type="email"
          value={emailDraft}
          onChange={(event) => setEmailDraft(event.target.value)}
        />
        <button
          type="button"
          className="button button--primary"
          disabled={signIn.isPending}
          onClick={() => signIn.mutate(emailDraft)}
        >
          Sign in
        </button>
        {signIn.isError && <p className="state state--error">That address is not allowed here.</p>}
      </section>
    );
  }

  return (
    <section className="console">
      <header className="console__header">
        <h1>Producer console</h1>
        <p className="console__user">{email}</p>
        <label htmlFor="console-client">Client</label>
        <input
          id="console-client"
          value={clientCode}
          onChange={(event) => dispatch(clientSelected(event.target.value))}
        />
        <button type="button" className="button" onClick={() => dispatch(signedOut())}>
          Sign out
        </button>
      </header>

      {outstanding.isPending && <p className="state">Loading…</p>}
      {outstanding.isError && <p className="state state--error">Could not load the work list.</p>}

      <ul className="console__list">
        {(outstanding.data ?? []).map((review) => (
          <li key={review.reviewId} className="console__item">
            <span className="console__campaign">{review.campaignName}</span>
            <span className="console__version">round {review.version}</span>
            <span className="console__status">{review.status}</span>
            {(review.status === 'Draft' || review.status === 'ChangesRequested') && (
              <button
                type="button"
                className="button button--primary"
                disabled={submit.isPending}
                onClick={() => submit.mutate(review.reviewId)}
              >
                Send for review
              </button>
            )}
          </li>
        ))}
      </ul>
    </section>
  );
}
