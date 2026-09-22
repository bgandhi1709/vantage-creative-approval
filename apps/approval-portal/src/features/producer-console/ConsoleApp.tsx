import { Provider } from 'react-redux';
import { consoleStore } from './core/state/consoleStore';
import { ConsolePage } from './pages/ConsolePage';

/**
 * The console mounts its own Redux Provider inside the portal's tree. React Query is shared —
 * it holds no identity — but state and credentials are not.
 */
export function ConsoleApp() {
  return (
    <Provider store={consoleStore}>
      <ConsolePage />
    </Provider>
  );
}
