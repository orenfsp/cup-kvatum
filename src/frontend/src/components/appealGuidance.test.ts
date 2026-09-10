import { describe, expect, it } from 'vitest';
import { getApplicantAppealGuidance } from './appealGuidance';

describe('getApplicantAppealGuidance', () => {
  it('turns a clarification status into one clear child action', () => {
    const guidance = getApplicantAppealGuidance('NeedsClarification', 'Student');

    expect(guidance.title).toBe('Специалист ждёт твоего ответа');
    expect(guidance.actionRequired).toBe(true);
    expect(guidance.actionLabel).toBe('Прочитать вопрос и ответить');
    expect(guidance.actionRoute).toBe('/appeal/dialog');
    expect(guidance.journey.filter((step) => step.state === 'current')).toHaveLength(1);
  });

  it('explains that a returned appeal has not disappeared', () => {
    const guidance = getApplicantAppealGuidance('Returned', 'Student');

    expect(guidance.description).toContain('Обращение не потерялось');
    expect(guidance.actionRequired).toBe(false);
  });

  it('sends a ready appeal to the specialist answer', () => {
    const guidance = getApplicantAppealGuidance('RecommendationReady', 'Parent');

    expect(guidance.title).toBe('Ответ специалиста готов');
    expect(guidance.actionRoute).toBe('/appeal/answer');
  });

  it('shows every journey step as complete for a closed appeal', () => {
    const guidance = getApplicantAppealGuidance('Closed', 'Teacher');

    expect(guidance.journey).toHaveLength(4);
    expect(guidance.journey.every((step) => step.state === 'done')).toBe(true);
    expect(guidance.actionRoute).toBe('/appeal/history');
  });

  it('does not invent a normal journey after rejection', () => {
    const guidance = getApplicantAppealGuidance('Rejected', 'Student');

    expect(guidance.journey).toHaveLength(0);
    expect(guidance.actionRoute).toBe('/appeal/new');
  });
});
