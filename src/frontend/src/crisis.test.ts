import { describe, expect, it } from 'vitest';
import { crisisTextMatches } from './crisis';

const patterns = ['меня избива*', 'угрожа* уб*', 'хочу умер*', 'не хочу жить'];

describe('crisisTextMatches', () => {
  it.each([
    'Меня избивают после школы',
    'Меня сейчас избивают после школы',
    'Мне угрожают убить',
    'Я хлчу умереть',
    'Я больше не хочу жить',
  ])('detects risk in %s', (text) => {
    expect(crisisTextMatches([text], patterns)).toBe(true);
  });

  it.each([
    'Мне нужна помощь с конфликтом в классе',
    'Я хочу спокойно поговорить с учителем',
    'На уроке меня дразнят',
  ])('keeps neutral text neutral in %s', (text) => {
    expect(crisisTextMatches([text], patterns)).toBe(false);
  });
});
