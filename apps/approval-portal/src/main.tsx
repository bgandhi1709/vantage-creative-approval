import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { RouterProvider } from 'react-router-dom';
import { AppProviders } from './app/AppProviders';
import { router } from './app/routes';
import './core/i18n';
import './styles/main.css';

const container = document.getElementById('root');

if (container === null) {
  throw new Error('The root element is missing from index.html.');
}

createRoot(container).render(
  <StrictMode>
    <AppProviders>
      <RouterProvider router={router} />
    </AppProviders>
  </StrictMode>,
);
