import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { describe, expect, it, vi } from 'vitest';
import { RoleNavigation, StaffAppShell, type StaffNavigationItem } from './AppShells';

const operatorNavigation: StaffNavigationItem[] = [
  { label: 'Очередь', to: '/staff/operator/queue' },
  { label: 'Срочная помощь', to: '/staff/operator/urgent' },
  { label: 'Аналитика', to: '/staff/analytics', analytics: true },
];

describe('URL-driven staff shell', () => {
  it('marks a detail route through the owning section link', () => {
    render(
      <MemoryRouter initialEntries={['/staff/operator/queue/appeal-1']}>
        <RoleNavigation
          id="test-navigation"
          label="Кабинет оператора"
          items={operatorNavigation}
          open
          onClose={vi.fn()}
        />
      </MemoryRouter>,
    );

    expect(screen.getByRole('link', { name: 'Очередь' })).toHaveAttribute('aria-current', 'page');
    expect(screen.getByRole('link', { name: 'Срочная помощь' })).toHaveAttribute('href', '/staff/operator/urgent');
  });

  it('keeps role navigation and the explicit mobile trigger in one shell', () => {
    render(
      <MemoryRouter initialEntries={['/staff/operator/urgent']}>
        <StaffAppShell
          displayName="Оператор"
          roleLabel="Кабинет оператора"
          roleClassName="operator"
          navigation={operatorNavigation}
          logoutPending={false}
          onLogout={vi.fn()}
        >
          <h1>Срочная помощь</h1>
        </StaffAppShell>
      </MemoryRouter>,
    );

    expect(screen.getByRole('button', { name: 'Разделы' })).toHaveAttribute('aria-expanded', 'false');
    expect(screen.getByRole('link', { name: 'Срочная помощь' })).toHaveAttribute('aria-current', 'page');
    expect(screen.getByRole('heading', { name: 'Срочная помощь' })).toBeInTheDocument();
  });

  it('keeps a case attached to its source section and renders live work counts', () => {
    const expertNavigation: StaffNavigationItem[] = [
      { label: 'Новые назначения', to: '/staff/expert/inbox', count: 2, activeMatch: { pathPrefix: '/staff/expert/cases', searchParam: 'from', value: 'inbox' } },
      { label: 'В работе', to: '/staff/expert/active', count: 4, meta: '4 требуют действия', attention: true, activeMatch: { pathPrefix: '/staff/expert/cases', searchParam: 'from', value: 'active' } },
    ];
    render(
      <MemoryRouter initialEntries={['/staff/expert/cases/appeal-1/dialog?from=active']}>
        <RoleNavigation
          id="expert-navigation"
          label="Кабинет эксперта"
          items={expertNavigation}
          open
          onClose={vi.fn()}
        />
      </MemoryRouter>,
    );

    const active = screen.getByRole('link', { name: 'В работе' });
    expect(active).toHaveAttribute('aria-current', 'page');
    expect(active.querySelector('[data-navigation-count]')).toHaveTextContent('4');
    expect(active).toHaveTextContent('4 требуют действия');
  });
});
