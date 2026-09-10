import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { CrisisHelpPanel } from './CrisisHelpPanel';

const contacts = [{
  displayName: 'Телефон доверия',
  displayNumber: '124',
  dialNumber: '124',
  description: 'Круглосуточно',
}];

describe('CrisisHelpPanel', () => {
  it('speaks to a student using informal Russian', () => {
    render(<CrisisHelpPanel applicantType="Student" contacts={contacts} />);

    expect(screen.getByText(/Перейди туда/u)).toBeVisible();
    expect(screen.getByText(/кто ты/u)).toBeVisible();
    expect(screen.queryByText(/Перейдите туда/u)).not.toBeInTheDocument();
  });

  it.each(['Parent', 'Teacher'] as const)('speaks to %s using formal Russian', (applicantType) => {
    render(<CrisisHelpPanel applicantType={applicantType} contacts={contacts} />);

    expect(screen.getByText(/Перейдите туда/u)).toBeVisible();
    expect(screen.getByText(/кто вы/u)).toBeVisible();
  });
});
