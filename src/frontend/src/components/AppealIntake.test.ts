import { describe, expect, it } from 'vitest';
import { copyForApplicant, validateAttachmentSelection } from './AppealIntake';

describe('copyForApplicant', () => {
  it('uses informal Russian for a student', () => {
    const copy = copyForApplicant('Student');

    expect(copy.pathHeading).toContain('тебе');
    expect(copy.narrativeHeading).toContain('Расскажи');
    expect(copy.optionalHint).toContain('можно пропустить');
  });

  it.each(['Parent', 'Teacher'] as const)('uses formal Russian for %s', (applicantType) => {
    const copy = copyForApplicant(applicantType);

    expect(copy.pathHeading).toContain('вам');
    expect(copy.narrativeHeading).toContain('Расскажите');
    expect(copy.optionalHint).toContain('можно пропустить');
  });
});

describe('validateAttachmentSelection', () => {
  it('rejects a sixth file', () => {
    const file = new File(['safe'], 'evidence.png', { type: 'image/png' });

    expect(validateAttachmentSelection(5, [file])).toContain('не больше пяти');
  });

  it('rejects a file over ten megabytes', () => {
    const file = new File([new Uint8Array(10 * 1024 * 1024 + 1)], 'large.jpg', { type: 'image/jpeg' });

    expect(validateAttachmentSelection(0, [file])).toContain('больше 10 МБ');
  });

  it('rejects a declared type that conflicts with the extension', () => {
    const file = new File(['safe'], 'disguised.png', { type: 'image/jpeg' });

    expect(validateAttachmentSelection(0, [file])).toContain('не подходит');
  });
});
