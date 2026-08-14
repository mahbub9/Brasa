const SEEDED_TABLE_LABEL = /^Mesa (\d+)$/;

/**
 * "Mesa 1".."Mesa 32" are DevFloorSeeder's placeholder labels (standing in
 * for FLR-03's real floor editor, see its own doc comment) — not yet
 * tenant-entered content the way a real restaurant's own table names would
 * be once that editor exists. "Mesa" is a generic word ("table"), not an
 * identity-bearing name the way a dish's is ("Bacalhau à Brás" stays
 * untranslated on purpose, see ADR 0011) — staff who don't read Portuguese
 * still need to recognise which table is which. Anything that doesn't match
 * the seeded shape (a future tenant's own custom label, or a takeaway
 * ticket's free-text label) passes through unchanged, same as menu content.
 */
export function formatTableLabel(
  label: string,
  t: (key: string, options?: Record<string, string | number>) => string,
): string {
  const match = SEEDED_TABLE_LABEL.exec(label);
  return match ? t('floor.tableLabel', { number: match[1] }) : label;
}
