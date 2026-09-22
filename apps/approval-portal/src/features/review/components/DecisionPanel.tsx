import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { DecisionOutcome } from '../api/reviewApi';

interface DecisionPanelProps {
  status: string;
  isSubmitting: boolean;
  onDecide: (outcome: DecisionOutcome, comment: string) => void;
}

/** Collects the answer and raises it as a typed callback; it decides nothing itself. */
export function DecisionPanel({ status, isSubmitting, onDecide }: DecisionPanelProps) {
  const { t } = useTranslation();
  const [comment, setComment] = useState('');

  if (status !== 'AwaitingDecision') {
    return <p className="state state--done">{t('review.decisionRecorded')}</p>;
  }

  return (
    <section className="decision">
      <label className="decision__label" htmlFor="decision-comment">
        {t('review.commentLabel')}
      </label>
      <textarea
        id="decision-comment"
        className="decision__comment"
        placeholder={t('review.commentPlaceholder')}
        value={comment}
        onChange={(event) => setComment(event.target.value)}
        rows={4}
      />
      <div className="decision__actions">
        <button
          type="button"
          className="button button--primary"
          disabled={isSubmitting}
          onClick={() => onDecide('Approved', comment)}
        >
          {t('review.approve')}
        </button>
        <button
          type="button"
          className="button"
          disabled={isSubmitting || comment.trim().length === 0}
          onClick={() => onDecide('ChangesRequested', comment)}
        >
          {t('review.requestChanges')}
        </button>
      </div>
      {isSubmitting && <p className="state">{t('review.submitting')}</p>}
    </section>
  );
}
