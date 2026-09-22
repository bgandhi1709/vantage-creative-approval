import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useParams } from 'react-router-dom';
import { useReviewLocale } from '@/core/i18n/useReviewLocale';
import { useAppDispatch } from '@/core/state/hooks';
import { reviewOpened } from '@/core/state/sessionSlice';
import { reviewApi, type DecisionOutcome } from '../api/reviewApi';
import { AssetList } from '../components/AssetList';
import { DecisionPanel } from '../components/DecisionPanel';

/**
 * The page a client reviewer lands on from their emailed link.
 *
 * It orchestrates: exchange the link for a session, load the round, render it in the campaign's
 * locale, and record the answer. The components below it take props and raise typed callbacks.
 */
export function ReviewPage() {
  const { clientCode = '', reviewId = '' } = useParams<{ clientCode: string; reviewId: string }>();
  const locale = useReviewLocale();
  const { t } = useTranslation();
  const dispatch = useAppDispatch();
  const queryClient = useQueryClient();
  const [sessionReady, setSessionReady] = useState(false);

  useEffect(() => {
    dispatch(reviewOpened({ clientCode, locale, reviewId }));
  }, [clientCode, dispatch, locale, reviewId]);

  // The link is the credential. It is exchanged once for the session cookie every later call uses.
  useEffect(() => {
    let cancelled = false;

    reviewApi
      .exchangeLink(reviewId)
      .catch(() => undefined)
      .finally(() => {
        if (!cancelled) {
          setSessionReady(true);
        }
      });

    return () => {
      cancelled = true;
    };
  }, [reviewId]);

  const review = useQuery({
    queryKey: ['review', clientCode, reviewId],
    queryFn: () => reviewApi.get(clientCode, reviewId),
    enabled: sessionReady && reviewId.length > 0,
  });

  const decide = useMutation({
    mutationFn: ({ outcome, comment }: { outcome: DecisionOutcome; comment: string }) =>
      reviewApi.decide(clientCode, reviewId, outcome, comment),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['review', clientCode, reviewId] });
    },
  });

  if (review.isPending) {
    return <p className="state">{t('common.loading')}</p>;
  }

  if (review.isError || review.data === undefined) {
    return (
      <div className="state state--error">
        <p>{t('review.loadFailed')}</p>
        <button type="button" onClick={() => void review.refetch()}>
          {t('common.retry')}
        </button>
      </div>
    );
  }

  const data = review.data;

  return (
    <article className="review">
      <header className="review__header">
        <p className="review__eyebrow">{data.campaignName}</p>
        <h1>{t('review.title', { version: data.version })}</h1>
        <dl className="review__meta">
          <div>
            <dt>{t('review.status')}</dt>
            <dd>{t(`status.${data.status}`)}</dd>
          </div>
          {data.sentDate !== null && (
            <div>
              <dt>{t('review.sent')}</dt>
              <dd>{new Date(data.sentDate).toLocaleDateString(locale)}</dd>
            </div>
          )}
        </dl>
      </header>

      <AssetList assets={data.assets} heading={t('review.assets')} />

      <DecisionPanel
        status={data.status}
        isSubmitting={decide.isPending}
        onDecide={(outcome, comment) => decide.mutate({ outcome, comment })}
      />
    </article>
  );
}
