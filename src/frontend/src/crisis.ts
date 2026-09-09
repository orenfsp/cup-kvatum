export function crisisTextMatches(textParts: Array<string | null | undefined>, patterns: string[]) {
  const textTokens = tokenize(textParts.filter(Boolean).join(' '));
  if (textTokens.length === 0) return false;

  return patterns.some((pattern) => {
    const patternTokens = tokenize(pattern);
    if (patternTokens.length === 0 || patternTokens.length > textTokens.length) return false;

    for (let start = 0; start < textTokens.length; start += 1) {
      if (!tokenMatches(patternTokens[0]!, textTokens[start]!)) continue;
      let candidateIndex = start;
      let matched = true;
      for (let patternIndex = 1; patternIndex < patternTokens.length; patternIndex += 1) {
        let foundAt = -1;
        const lastCandidate = Math.min(textTokens.length - 1, candidateIndex + 3);
        for (let next = candidateIndex + 1; next <= lastCandidate; next += 1) {
          if (!tokenMatches(patternTokens[patternIndex]!, textTokens[next]!)) continue;
          foundAt = next;
          break;
        }
        if (foundAt < 0) {
          matched = false;
          break;
        }
        candidateIndex = foundAt;
      }
      if (matched) return true;
    }
    return false;
  });
}

function tokenize(value: string) {
  return value
    .toLocaleLowerCase('ru-RU')
    .replaceAll('ё', 'е')
    .match(/[\p{L}\p{N}*]+/gu) ?? [];
}

function tokenMatches(pattern: string, candidate: string) {
  if (pattern.endsWith('*')) {
    const stem = pattern.slice(0, -1);
    if (candidate.startsWith(stem)) return true;
    return stem.length >= 4
      && candidate.length >= stem.length
      && editDistanceAtMostOne(stem, candidate.slice(0, stem.length));
  }
  if (pattern === candidate) return true;
  return pattern.length >= 4 && candidate.length >= 4 && editDistanceAtMostOne(pattern, candidate);
}

function editDistanceAtMostOne(left: string, right: string) {
  if (Math.abs(left.length - right.length) > 1) return false;
  let leftIndex = 0;
  let rightIndex = 0;
  let edits = 0;

  while (leftIndex < left.length && rightIndex < right.length) {
    if (left[leftIndex] === right[rightIndex]) {
      leftIndex += 1;
      rightIndex += 1;
      continue;
    }
    edits += 1;
    if (edits > 1) return false;
    if (left.length > right.length) leftIndex += 1;
    else if (right.length > left.length) rightIndex += 1;
    else {
      leftIndex += 1;
      rightIndex += 1;
    }
  }

  return edits + (leftIndex < left.length || rightIndex < right.length ? 1 : 0) <= 1;
}
