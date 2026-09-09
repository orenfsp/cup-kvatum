import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { AdminWorkspace } from './AdminWorkspace';
import { getAdminConfiguration } from '../api/admin';

vi.mock('@material/web/button/filled-button.js', () => ({}));
vi.mock('@material/web/button/outlined-button.js', () => ({}));

vi.mock('../api/admin', () => ({
  adminError: () => 'Ошибка',
  changeAdminUserState: vi.fn(),
  createAdminCategory: vi.fn(),
  createAdminGroup: vi.fn(),
  createAdminUser: vi.fn(),
  deactivateAdminCategory: vi.fn(),
  deactivateAdminGroup: vi.fn(),
  deactivateAdminRule: vi.fn(),
  getAdminAudit: vi.fn(),
  getAdminAuditDetail: vi.fn(),
  getAdminConfiguration: vi.fn(),
  getAdminUsers: vi.fn(),
  getStuckAppeals: vi.fn(),
  interveneInStuckAppeal: vi.fn(),
  saveAdminRule: vi.fn(),
  updateAdminCategory: vi.fn(),
  updateAdminGroup: vi.fn(),
  updateAdminUser: vi.fn(),
}));

describe('AdminWorkspace', () => {
  it('presents configuration as one calm privacy-bounded workflow', async () => {
    vi.mocked(getAdminConfiguration).mockResolvedValue({
      categories: [{
        id: 'category-1',
        code: 'support',
        displayName: 'Поддержка',
        sortOrder: 10,
        isActive: true,
        version: 1,
        updatedAt: '2026-09-09T00:00:00Z',
        usageCount: 0,
      }],
      groups: [{
        id: 'group-1',
        code: 'experts',
        displayName: 'Группа поддержки',
        activeAppealLimit: 5,
        isActive: true,
        version: 1,
        updatedAt: '2026-09-09T00:00:00Z',
        memberIds: [],
      }],
      rules: [],
      experts: [],
    });
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });

    render(
      <QueryClientProvider client={client}>
        <AdminWorkspace view="configuration" />
      </QueryClientProvider>,
    );

    expect(await screen.findByRole('region', { name: 'Категории' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Конфигурация маршрутизации' })).toBeInTheDocument();
    expect(screen.getByRole('region', { name: 'Группы экспертов' })).toBeInTheDocument();
    expect(screen.getByRole('region', { name: 'Правила маршрутизации' })).toBeInTheDocument();
    expect(screen.getByText(/Тексты обращений, чат, заметки, вложения/)).toBeInTheDocument();
  });
});
