/**
 * The shape every client's own `MoneyDto` already has. Defined locally
 * rather than imported from `pos`/`admin`'s own hand-written `api/types.ts`
 * (WEB-02) — this package has no business depending on either app's
 * specific API contract, and TypeScript's structural typing means a real
 * `MoneyDto` from either app satisfies this without any cast.
 */
export interface MoneyLike {
  amount: number;
  currency: string;
}

const formatters = new Map<string, Intl.NumberFormat>();

/**
 * Formats a MoneyLike the way a Portuguese guest expects to see it (e.g. "9,50 €").
 *
 * Deliberately always 'pt-PT', never the UI language toggle — an
 * English-speaking staff member switching the interface to English must not
 * change how a total on a table's screen or a printed receipt reads. Money
 * formatting is a fiscal/business concern, not UI chrome, the same
 * distinction the receipt's issued-date formatting makes. See
 * docs/architecture/decisions/0011-i18n.md.
 */
export function formatMoney(money: MoneyLike): string {
  let formatter = formatters.get(money.currency);
  if (!formatter) {
    formatter = new Intl.NumberFormat('pt-PT', { style: 'currency', currency: money.currency });
    formatters.set(money.currency, formatter);
  }
  return formatter.format(money.amount);
}
