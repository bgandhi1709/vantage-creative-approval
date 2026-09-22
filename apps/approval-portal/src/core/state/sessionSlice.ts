import { createSlice, type PayloadAction } from '@reduxjs/toolkit';

/**
 * Storage key for the client-facing session.
 *
 * The producer console keeps its own store under its own key (see the console's sessionSlice).
 * The two must never share a key: one browser can hold a client reviewer session and a producer
 * session at the same time, and a shared key would let one overwrite the other.
 */
export const REVIEWER_SESSION_KEY = 'vantage.reviewer.session';

export interface ReviewerSessionState {
  clientCode: string | null;
  locale: string;
  reviewId: string | null;
  reviewerName: string | null;
}

const initialState: ReviewerSessionState = loadState();

function loadState(): ReviewerSessionState {
  const empty: ReviewerSessionState = {
    clientCode: null,
    locale: 'en-US',
    reviewId: null,
    reviewerName: null,
  };

  try {
    const stored = window.sessionStorage.getItem(REVIEWER_SESSION_KEY);
    return stored === null ? empty : ({ ...empty, ...JSON.parse(stored) } as ReviewerSessionState);
  } catch {
    // Private windows and blocked site data both throw here. The portal works without it.
    return empty;
  }
}

function persist(state: ReviewerSessionState): void {
  try {
    window.sessionStorage.setItem(REVIEWER_SESSION_KEY, JSON.stringify(state));
  } catch {
    // Nothing here is required for the page to function.
  }
}

export const sessionSlice = createSlice({
  name: 'reviewerSession',
  initialState,
  reducers: {
    reviewOpened(
      state,
      action: PayloadAction<{ clientCode: string; locale: string; reviewId: string }>,
    ) {
      state.clientCode = action.payload.clientCode;
      state.locale = action.payload.locale;
      state.reviewId = action.payload.reviewId;
      persist(state);
    },
    reviewerNamed(state, action: PayloadAction<string>) {
      state.reviewerName = action.payload;
      persist(state);
    },
    sessionCleared(state) {
      state.clientCode = null;
      state.reviewId = null;
      state.reviewerName = null;
      persist(state);
    },
  },
});

export const { reviewOpened, reviewerNamed, sessionCleared } = sessionSlice.actions;
