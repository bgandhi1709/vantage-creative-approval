import { consoleBffClient } from '../http/consoleBffClient';
import type { Review } from '@/features/review/api/reviewApi';

export const consoleApi = {
  signIn: (email: string) =>
    consoleBffClient.post<{ accessToken: string; tokenType: string }>('/auth/producer-token', {
      email,
    }),

  getOutstanding: (clientCode: string) =>
    consoleBffClient.get<Review[]>(`/v1/${clientCode}/reviews`),

  createDraft: (
    clientCode: string,
    body: {
      campaignId: string;
      campaignName: string;
      reviewerEmail: string;
      reviewerName: string;
      locale: string;
      assets: { name: string; format: string; previewPath: string }[];
    },
  ) => consoleBffClient.post<Review>(`/v1/${clientCode}/reviews`, body),

  submit: (clientCode: string, reviewId: string) =>
    consoleBffClient.post<Review>(`/v1/${clientCode}/reviews/${reviewId}/submit`),
};
