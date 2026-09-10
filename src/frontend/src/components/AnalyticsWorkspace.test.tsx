import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { describe, expect, it, vi } from 'vitest';
import { getAnalyticsDashboard } from '../api/analytics';
import { AnalyticsWorkspace } from './AnalyticsWorkspace';

vi.mock('@material/web/button/filled-button.js', () => ({}));
vi.mock('@material/web/button/outlined-button.js', () => ({}));

vi.mock('../api/analytics', () => ({
  analyticsError: () => 'Ошибка',
  createAnalyticsExport: vi.fn(),
  downloadAnalyticsExport: vi.fn(),
  getAnalyticsDashboard: vi.fn(),
}));

describe('AnalyticsWorkspace', () => {
  it('explains the privacy boundary and shows metadata-only metrics', async () => {
    vi.mocked(getAnalyticsDashboard).mockResolvedValue({
      scope: { role: 'Administrator', id: 'admin', label: 'Все обращения' },
      period: { from: '2026-08-10T00:00:00Z', to: '2026-09-09T00:00:00Z' },
      generatedAt: '2026-09-09T00:00:00Z',
      total: 12,
      active: 4,
      urgentSharePercent: 8.3,
      returnedSharePercent: 16.7,
      averageMinutes: { operatorAccepted: 15, firstExpertResponse: 62, closed: 1440 },
      distributions: {
        status: [{ key: 'New', label: 'Новое', count: 4 }],
        priority: [{ key: 'Standard', label: 'Обычный', count: 12 }],
        applicantType: [{ key: 'Student', label: 'Ученик', count: 12 }],
        category: [{ key: 'category', label: 'Травля', count: 12 }],
      },
      workload: [{ staffUserId: 'expert', displayName: 'Эксперт', activeCount: 4 }],
      definitions: { total: 'Обращения, созданные в выбранном периоде.' },
    });
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });

    render(
      <MemoryRouter>
        <QueryClientProvider client={client}><AnalyticsWorkspace /></QueryClientProvider>
      </MemoryRouter>,
    );

    expect(await screen.findByRole('heading', { name: 'Аналитика' })).toBeInTheDocument();
    expect(screen.getByText(/Тексты, чат, заметки, файлы/)).toBeInTheDocument();
    expect(await screen.findByRole('region', { name: 'Ключевые показатели' })).toHaveTextContent('12');
    expect(screen.getByRole('region', { name: 'По категориям' })).toHaveTextContent('Травля');
    expect(screen.getByText('Скачать CSV')).toBeInTheDocument();
    expect(screen.getByText('Скачать XLSX')).toBeInTheDocument();
  });
});
