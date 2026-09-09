import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { describe, expect, it } from 'vitest';
import { PublicFrame } from './PublicFrame';

describe('PublicFrame', () => {
  it('keeps the two applicant routes visible and marks the current route', () => {
    render(<MemoryRouter initialEntries={['/appeal']}><PublicFrame current="status"><main>Содержимое</main></PublicFrame></MemoryRouter>);

    expect(screen.getByRole('navigation', { name: 'Основная навигация' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Обратиться' })).toHaveAttribute('href', '/appeal/new');
    expect(screen.getByRole('link', { name: 'Моё обращение' })).toHaveAttribute('aria-current', 'page');
    expect(screen.getByRole('link', { name: 'Вход для сотрудников' })).toHaveAttribute('href', '/staff');
  });

  it('keeps system health outside primary navigation', () => {
    render(<MemoryRouter><PublicFrame current="home"><main>Содержимое</main></PublicFrame></MemoryRouter>);

    expect(screen.getByRole('navigation', { name: 'Основная навигация' }))
      .not.toContainElement(screen.getByRole('link', { name: 'Состояние сервиса' }));
  });
});
