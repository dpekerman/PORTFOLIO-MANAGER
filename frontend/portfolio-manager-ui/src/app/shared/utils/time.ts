/** Converts a backend "HH:mm" string to a Date for MatTimepickerInput (null when invalid). */
export function timeStrToDate(timeStr: string | null | undefined): Date | null {
  if (!timeStr) return null;
  const parts = timeStr.split(':').map(Number);
  if (parts.length < 2 || isNaN(parts[0]) || isNaN(parts[1])) return null;
  const d = new Date();
  d.setHours(parts[0], parts[1], 0, 0);
  return d;
}

/** Converts a MatTimepickerInput Date back to the backend "HH:mm" string. */
export function dateToTimeString(date: Date | null | undefined): string {
  if (!date) return '00:00';
  return `${date.getHours().toString().padStart(2, '0')}:${date.getMinutes().toString().padStart(2, '0')}`;
}
