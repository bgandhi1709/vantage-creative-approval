import { Outlet } from 'react-router-dom';

export function ReviewLayout() {
  return (
    <div className="shell">
      <header className="shell__header">
        <span className="shell__brand">Vantage Works</span>
      </header>
      <main className="shell__main">
        <Outlet />
      </main>
      <footer className="shell__footer">
        <span>Creative approval</span>
      </footer>
    </div>
  );
}
