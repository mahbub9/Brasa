import type { MoneyDto } from '../api/types';

const formatters = new Map<string, Intl.NumberFormat>();

/** Formats a MoneyDto the way a Portuguese guest expects to see it (e.g. "9,50 €"). */
export function formatMoney(money: MoneyDto): string {
  let formatter = formatters.get(money.currency);
  if (!formatter) {
    formatter = new Intl.NumberFormat('pt-PT', { style: 'currency', currency: money.currency });
    formatters.set(money.currency, formatter);
  }
  return formatter.format(money.amount);
}
