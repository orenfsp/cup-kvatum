import { useEffect, useRef, useState, type ReactNode } from 'react';
import { Link, useLocation } from 'react-router-dom';

export type StaffNavigationItem = {
  label: string;
  to: string;
  count?: number;
  meta?: string;
  attention?: boolean;
  analytics?: boolean;
  activePrefixes?: string[];
  activeMatch?: { pathPrefix: string; searchParam: string; value: string };
  end?: boolean;
};

export function PublicAppShell({ children }: { children: ReactNode }) {
  return (
    <div className="public-shell">
      <PublicHeader />
      {children}
      <footer className="app-footer">
        <p>Отклик — анонимная помощь без регистрации</p>
        <div className="app-footer__links">
          <Link to="/staff">Вход для сотрудников</Link>
          <Link to="/system">Состояние сервиса</Link>
        </div>
      </footer>
    </div>
  );
}

export function PublicHeader() {
  const location = useLocation();
  const inIntake = location.pathname === '/appeal/new';
  const inAppeal = location.pathname.startsWith('/appeal') && !inIntake;
  return (
    <header className="app-header">
      <Link className="brand" to="/">Отклик</Link>
      <nav className="primary-nav" aria-label="Основная навигация">
        <Link className="primary-nav__link" to="/appeal/new" aria-current={inIntake ? 'page' : undefined}>Обратиться</Link>
        <Link className="primary-nav__link" to="/appeal" aria-current={inAppeal ? 'page' : undefined}>Моё обращение</Link>
      </nav>
    </header>
  );
}

export function StaffAppShell({
  children,
  displayName,
  roleLabel,
  roleClassName,
  navigation,
  onLogout,
  logoutPending,
}: {
  children: ReactNode;
  displayName: string;
  roleLabel: string;
  roleClassName: string;
  navigation: StaffNavigationItem[];
  onLogout: () => void;
  logoutPending: boolean;
}) {
  const [navigationOpen, setNavigationOpen] = useState(false);
  const location = useLocation();
  const mainRef = useRef<HTMLElement>(null);

  useEffect(() => {
    setNavigationOpen(false);
    requestAnimationFrame(() => {
      const main = mainRef.current;
      if (!main || main.querySelector('[data-detail-heading], .page-header .staff-title')) return;
      main.focus({ preventScroll: true });
    });
  }, [location.pathname]);

  return (
    <div className={`staff-shell staff-shell--${roleClassName}`}>
      <header className="staff-topbar">
        <div className="staff-context">
          <Link className="brand" to="/">Отклик</Link>
          <span>Сотрудникам</span>
        </div>
        <button
          className="staff-menu-trigger"
          type="button"
          aria-expanded={navigationOpen}
          aria-controls="staff-role-navigation"
          onClick={() => setNavigationOpen(true)}
        >
          Разделы
        </button>
        <div className="staff-profile">
          <span>{displayName}</span>
          <md-outlined-button disabled={logoutPending} onClick={onLogout}>
            Выйти
          </md-outlined-button>
        </div>
      </header>
      <div className="staff-layout">
        {navigationOpen ? (
          <button
            className="staff-navigation-backdrop"
            type="button"
            aria-label="Закрыть разделы"
            onClick={() => setNavigationOpen(false)}
          />
        ) : null}
        <RoleNavigation
          id="staff-role-navigation"
          label={roleLabel}
          items={navigation}
          open={navigationOpen}
          onClose={() => setNavigationOpen(false)}
        />
        <main className="staff-main" ref={mainRef} tabIndex={-1}>
          {children}
        </main>
      </div>
    </div>
  );
}

export function RoleNavigation({
  id,
  label,
  items,
  open,
  onClose,
}: {
  id: string;
  label: string;
  items: StaffNavigationItem[];
  open: boolean;
  onClose: () => void;
}) {
  const location = useLocation();

  return (
    <nav
      id={id}
      className={`staff-nav${open ? ' staff-nav--open' : ''}`}
      aria-label="Разделы кабинета"
    >
      <div className="staff-nav__heading">
        <p className="eyebrow">{label}</p>
        <button type="button" onClick={onClose}>Закрыть</button>
      </div>
      {items.map((item) => {
        const prefixActive = item.activePrefixes?.some((prefix) => location.pathname.startsWith(prefix));
        const searchActive = item.activeMatch
          && location.pathname.startsWith(item.activeMatch.pathPrefix)
          && new URLSearchParams(location.search).get(item.activeMatch.searchParam) === item.activeMatch.value;
        const routeActive = item.end
          ? location.pathname === item.to
          : location.pathname === item.to || location.pathname.startsWith(`${item.to}/`);
        const active = routeActive || Boolean(prefixActive) || Boolean(searchActive);
        const countId = item.count === undefined ? undefined : `${id}-${item.to.split('/').at(-1)}-count`;
        return (
          <Link
            className={[
              'staff-nav__item',
              item.analytics ? 'staff-nav__item--analytics' : '',
              item.attention ? 'staff-nav__item--attention' : '',
              active ? 'staff-nav__item--active' : '',
            ].filter(Boolean).join(' ')}
            key={item.to}
            to={item.to}
            aria-label={item.label}
            aria-describedby={countId}
            aria-current={active ? 'page' : undefined}
          >
            <span className="staff-nav__item-label">{item.label}</span>
            {item.count !== undefined ? (
              <strong className="staff-nav__count" id={countId} data-navigation-count aria-live="polite">
                {item.count}
              </strong>
            ) : null}
            {item.meta ? <small className="staff-nav__meta">{item.meta}</small> : null}
          </Link>
        );
      })}
    </nav>
  );
}
