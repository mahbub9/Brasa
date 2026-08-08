import { useTranslation } from 'react-i18next';
import type { MenuCategoryDto } from '../api/types';
import { formatMoney } from '../lib/money';

interface MenuGridProps {
  categories: MenuCategoryDto[];
  onAddItem: (menuItemId: string) => void;
  disabled: boolean;
}

export function MenuGrid({ categories, onAddItem, disabled }: MenuGridProps) {
  const { t } = useTranslation();

  if (categories.length === 0) {
    return <p className="empty-state">{t('menu.empty')}</p>;
  }

  return (
    <div className="menu-grid">
      {categories.map((category) => (
        <section key={category.id} className="menu-category">
          <h2>{category.name}</h2>
          <div className="menu-items">
            {category.items.map((item) => (
              <button
                key={item.id}
                type="button"
                className="menu-item"
                disabled={disabled}
                onClick={() => onAddItem(item.id)}
              >
                <span className="menu-item-name">{item.name}</span>
                <span className="menu-item-price">{formatMoney(item.price)}</span>
              </button>
            ))}
          </div>
        </section>
      ))}
    </div>
  );
}
