import { configureStore } from '@reduxjs/toolkit';
import { sessionSlice } from './sessionSlice';

/** The client-facing store. The producer console builds its own; see its core/state/store.ts. */
export const store = configureStore({
  reducer: {
    session: sessionSlice.reducer,
  },
});

export type RootState = ReturnType<typeof store.getState>;
export type AppDispatch = typeof store.dispatch;
