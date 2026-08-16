const SEEDED_TABLE_LABEL = /^Mesa (\d+)$/;

/**
 * Mirrors `pos`'s `src/lib/tableLabel.ts` (ADR 0011) — "Mesa 1".."Mesa 32"
 * are DevFloorSeeder's placeholder labels, not yet tenant-entered content,
 * so they're display-translated the same way here. A future tenant's own
 * custom label doesn't match the seeded shape and passes through unchanged.
 */
export function formatTableLabel(
  label: string,
  t: (key: string, options?: Record<string, string | number>) => string,
): string {
  const match = SEEDED_TABLE_LABEL.exec(label);
  return match ? t('floor.tableLabel', { number: match[1] }) : label;
}
