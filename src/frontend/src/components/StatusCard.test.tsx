import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { StatusCard } from './StatusCard';

describe('StatusCard', () => {
  it('renders a human-readable healthy state', () => {
    render(<StatusCard label="PostgreSQL" status="healthy" description="PostgreSQL отвечает" />);

    expect(screen.getByText('PostgreSQL')).toBeInTheDocument();
    expect(screen.getByText('Работает')).toBeInTheDocument();
    expect(screen.getByText('PostgreSQL отвечает')).toBeInTheDocument();
  });
});

