import { bffClient } from '@/core/http/bffClient';

/**
 * Thin wrappers over the BFF. No React, no state, no business rules: pages decide when to call
 * these and what to do with the answer.
 */

export interface CreativeAsset {
  assetId: string;
  name: string;
  format: string;
  previewPath: string;
}

export interface Review {
  reviewId: string;
  campaignId: string;
  clientCode: string;
  campaignName: string;
  version: number;
  status: string;
  locale: string;
  reviewerName: string;
  sentDate: string | null;
  lastModifiedUtc: string;
  assets: CreativeAsset[];
}

export interface ReviewNote {
  noteId: string;
  reviewId: string;
  authorEmail: string;
  body: string;
  createdUtc: string;
  isInternal: boolean;
}

export type DecisionOutcome = 'Approved' | 'ChangesRequested';

export const reviewApi = {
  exchangeLink: (reviewId: string) =>
    bffClient.post<void>('/auth/reviewer-session', { reviewId }),

  get: (clientCode: string, reviewId: string) =>
    bffClient.get<Review>(`/v1/${clientCode}/reviews/${reviewId}`),

  decide: (
    clientCode: string,
    reviewId: string,
    outcome: DecisionOutcome,
    comment: string,
    assetIds: string[] = [],
  ) =>
    bffClient.post<Review>(`/v1/${clientCode}/reviews/${reviewId}/decision`, {
      outcome,
      comment,
      assetIds,
    }),

  getNotes: (clientCode: string, reviewId: string) =>
    bffClient.get<ReviewNote[]>(`/v1/${clientCode}/reviews/${reviewId}/notes`),

  addNote: (clientCode: string, reviewId: string, body: string) =>
    bffClient.post<ReviewNote>(`/v1/${clientCode}/reviews/${reviewId}/notes`, {
      body,
      isInternal: false,
    }),
};
