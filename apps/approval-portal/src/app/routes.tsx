import { createBrowserRouter } from 'react-router-dom';
import { ConsoleApp } from '@/features/producer-console/ConsoleApp';
import { ReviewPage } from '@/features/review/pages/ReviewPage';
import { ReviewLayout } from './layouts/ReviewLayout';
import { NotFoundPage } from './layouts/NotFoundPage';

/**
 * Console routes are listed first so /console can never be swallowed by the locale-prefixed
 * client routes below it.
 */
export const router = createBrowserRouter([
  {
    path: '/console',
    element: <ConsoleApp />,
  },
  {
    path: '/:locale/:clientCode',
    element: <ReviewLayout />,
    children: [
      {
        path: 'review/:reviewId',
        element: <ReviewPage />,
      },
    ],
  },
  {
    path: '*',
    element: <NotFoundPage />,
  },
]);
