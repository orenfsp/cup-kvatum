import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen } from '@testing-library/react';
import { createMemoryRouter, RouterProvider } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { getAdminAudit, getAdminAuditDetail, getAdminConfiguration, getAdminUsers, getStuckAppeals } from '../api/admin';
import { AdminWorkspace, type AdminView } from './AdminWorkspace';

vi.mock('@material/web/button/filled-button.js', () => ({}));
vi.mock('@material/web/button/outlined-button.js', () => ({}));
vi.mock('../api/admin', () => ({
  adminError: () => 'Ошибка', changeAdminUserState: vi.fn(), createAdminCategory: vi.fn(), createAdminGroup: vi.fn(), createAdminUser: vi.fn(),
  deactivateAdminCategory: vi.fn(), deactivateAdminGroup: vi.fn(), deactivateAdminRule: vi.fn(), getAdminAudit: vi.fn(), getAdminAuditDetail: vi.fn(),
  getAdminConfiguration: vi.fn(), getAdminUsers: vi.fn(), getStuckAppeals: vi.fn(), interveneInStuckAppeal: vi.fn(), saveAdminRule: vi.fn(),
  updateAdminCategory: vi.fn(), updateAdminGroup: vi.fn(), updateAdminUser: vi.fn(),
}));

const configuration = {
  categories: [{ id: 'category-1', code: 'support', displayName: 'Поддержка', sortOrder: 10, isActive: true, version: 1, updatedAt: '2026-09-09T00:00:00Z', usageCount: 2 }],
  groups: [{ id: 'group-1', code: 'experts', displayName: 'Группа поддержки', activeAppealLimit: 5, isActive: true, version: 1, updatedAt: '2026-09-09T00:00:00Z', memberIds: ['expert-1'] }],
  rules: [{ id: 'rule-1', categoryId: 'category-1', category: 'Поддержка', expertGroupId: 'group-1', expertGroup: 'Группа поддержки', version: 2, isActive: true, updatedAt: '2026-09-09T00:00:00Z' }],
  experts: [{ id: 'expert-1', userName: 'expert', displayName: 'Эксперт', isActive: true, isAvailable: true }],
};

describe('AdminWorkspace', () => {
  beforeEach(() => {
    vi.mocked(getAdminConfiguration).mockResolvedValue(configuration);
    vi.mocked(getAdminUsers).mockResolvedValue({ total: 0, items: [] });
    vi.mocked(getStuckAppeals).mockResolvedValue({ total: 0, stuckHours: 4, items: [] });
    vi.mocked(getAdminAudit).mockResolvedValue({ total: 0, items: [], filters: { actions: [], targetTypes: [] } });
  });

  it('keeps categories in their own list/detail route with linked configuration', async () => {
    renderAdmin('categories', '/staff/admin/categories/category-1');
    expect(await screen.findByRole('heading', { name: 'Категории обращений' })).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Группы экспертов' })).not.toBeInTheDocument();
    expect(await screen.findByRole('heading', { name: 'Поддержка' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Группа поддержки · версия 2' })).toHaveAttribute('href', '/staff/admin/routing-rules/rule-1');
  });

  it('explains impact before category deactivation', async () => {
    renderAdmin('categories', '/staff/admin/categories/category-1');
    await screen.findByRole('heading', { name: 'Поддержка' });
    fireEvent.click(screen.getByText('Деактивировать', { selector: 'md-outlined-button' }));
    expect(screen.getByRole('dialog', { name: 'Деактивировать категорию?' })).toHaveTextContent('ранее созданных обращениях название и история сохранятся');
  });

  it('renders audit metadata as named fields without raw JSON', async () => {
    vi.mocked(getAdminAuditDetail).mockResolvedValue({
      id: 'event-1', actorUserId: 'admin-1', actorDisplayName: 'Администратор', action: 'StuckAppealIntervened', targetType: 'Appeal', targetId: 'appeal-1', reason: 'Возврат процесса в работу', occurredAt: '2026-09-09T00:00:00Z',
      before: { status: 'New', priority: 'Standard', version: 1 }, after: { status: 'Triaged', priority: 'Low', version: 2 },
    });
    renderAdmin('audit', '/staff/admin/audit/event-1');
    expect(await screen.findByRole('heading', { name: 'Зависшее обращение разблокировано' })).toBeInTheDocument();
    expect(screen.getAllByText('Статус')).toHaveLength(2);
    expect(document.querySelector('pre')).not.toBeInTheDocument();
    expect(document.body.textContent).not.toContain('{"status"');
    expect(screen.getByText(/Тексты обращений, чат, заметки/)).toBeInTheDocument();
  });
});

function renderAdmin(view: AdminView, initialEntry: string) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const paths: Record<AdminView, string> = {
    overview: '/staff/admin', categories: '/staff/admin/categories/:categoryId', groups: '/staff/admin/expert-groups/:groupId',
    rules: '/staff/admin/routing-rules/:ruleId', users: '/staff/admin/users/:userId', stuck: '/staff/admin/stuck/:appealId', audit: '/staff/admin/audit/:eventId',
  };
  const router = createMemoryRouter([{ path: paths[view], element: <AdminWorkspace view={view} /> }], { initialEntries: [initialEntry] });
  return render(<QueryClientProvider client={client}><RouterProvider router={router} /></QueryClientProvider>);
}
