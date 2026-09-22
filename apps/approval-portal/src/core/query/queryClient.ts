import { QueryClient } from '@tanstack/react-query';

/**
 * One client for the whole portal. Review data changes when a producer sends a new round, which is
 * measured in minutes, so a 30 second stale window and a single retry are enough; refetching on
 * every window focus would only add load.
 */
export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 30_000,
      retry: 1,
      refetchOnWindowFocus: false,
    },
  },
});
