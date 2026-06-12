export function formatSize(bytes: number): string {
  const units = ['B', 'KB', 'MB', 'GB', 'TB'];
  let value = bytes;
  let unit = 0;
  while (value >= 1024 && unit < units.length - 1) {
    value /= 1024;
    unit++;
  }
  const rendered = unit === 0 ? value.toString() : value.toFixed(1);
  return `${rendered} ${units[unit]}`;
}

export function formatModified(iso: string): string {
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return iso;
  return date.toLocaleString();
}

export function errorMessage(error: unknown): string {
  return error instanceof Error ? error.message : String(error);
}
