import type { MoneyDto } from '../api/types';

const formatters = new Map<string, Intl.NumberFormat>();

/**
 * Formats a MoneyDto the way a Portuguese guest expects to see it (e.g. "9,50 €").
 *
 * Deliberately always 'pt-PT', never the UI language toggle (src/i18n) — an
 * English-speaking staff member switching the interface to English must not
 * change how a total on a table's screen or a printed receipt reads. Money
 * formatting is a fiscal/business concern, not UI chrome, the same
 * distinction the receipt's issued-date formatting makes. See
 * docs/architecture/decisions/0011-i18n.md.
 */
export function formatMoney(money: MoneyDto): string {
  let formatter = formatters.get(money.currency);
  if (!formatter) {
    formatter = new Intl.NumberFormat('pt-PT', { style: 'currency', currency: money.currency });
    formatters.set(money.currency, formatter);
  }
  return formatter.format(money.amount);
}
