export function formatTrendShift(value: string | null | undefined, fallback = 'Waiting'): string {
  const cleaned = (value ?? '')
    .replace(/^\?\?\s*/, '')
    .replace(/^[\p{Emoji}\s]+/u, '')
    .trim();

  return cleaned || fallback;
}
