import { useTranslation } from 'react-i18next';
import type { MenuItemDto } from '../api/types';

interface CouvertBarProps {
  items: MenuItemDto[];
  coverCount: number;
  disabled: boolean;
  onAdd: (item: MenuItemDto) => void;
}

/**
 * One-tap couvert (CAT-12) — bread, olives and the like, offered at the
 * table before ordering. A normal menu item flagged `isCouvert` (admin,
 * WEB-10) still exists on the regular MenuGrid too; this is purely a
 * shortcut that rings it up at the order's own cover count instead of the
 * usual quantity-1 tap, so accepting couvert for the whole table is one tap
 * instead of one per guest. Charging happens the same way as any other
 * line — only when this is actually tapped, never automatically — which is
 * what "charged only when consumed" already means for every menu item.
 */
export function CouvertBar({ items, coverCount, disabled, onAdd }: CouvertBarProps) {
  const { t } = useTranslation();

  if (items.length === 0) {
    return null;
  }

  return (
    <section className="couvert-bar" data-testid="couvert-bar">
      <h2>{t('couvert.title')}</h2>
      <div className="menu-items">
        {items.map((item) => (
          <button
            key={item.id}
            type="button"
            className="menu-item"
            data-testid={`couvert-add-${item.name}`}
            disabled={disabled}
            onClick={() => onAdd(item)}
          >
            <span className="menu-item-name">{item.name}</span>
            <span className="menu-item-price">{t('couvert.add', { count: coverCount })}</span>
          </button>
        ))}
      </div>
    </section>
  );
}
