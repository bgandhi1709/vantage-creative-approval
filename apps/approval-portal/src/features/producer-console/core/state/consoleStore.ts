import { configureStore } from '@reduxjs/toolkit';
import { useDispatch, useSelector } from 'react-redux';
import { consoleSessionSlice } from './consoleSessionSlice';

/**
 * The console's own store. It is created separately from the portal store so console state can
 * never leak into a client-facing page, and so the two can be reasoned about independently.
 */
export const consoleStore = configureStore({
  reducer: {
    session: consoleSessionSlice.reducer,
  },
});

export type ConsoleState = ReturnType<typeof consoleStore.getState>;
export type ConsoleDispatch = typeof consoleStore.dispatch;

export const useConsoleDispatch = () => useDispatch<ConsoleDispatch>();
export const useConsoleSelector = useSelector.withTypes<ConsoleState>();
