import { formatMoney } from '@brasa/ui/lib/money';
import { useTranslation } from 'react-i18next';
import { apiOrigin } from '../api/client';
import type { MenuCategoryDto, MenuItemDto } from '../api/types';

interface MenuGridProps {
  categories: MenuCategoryDto[];
  onSelectItem: (item: MenuItemDto) => void;
  disabled: boolean;
  /** Whether the current order is takeaway (CAT-06) — picks `takeawayPrice` over `price` when set. */
  isTakeaway: boolean;
}

export function MenuGrid({ categories, onSelectItem, disabled, isTakeaway }: MenuGridProps) {
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
                onClick={() => onSelectItem(item)}
              >
                {item.imageUrl && (
                  <img src={`${apiOrigin}${item.imageUrl}`} alt="" className="menu-item-image" data-testid={`menu-item-image-${item.name}`} />
                )}
                <span className="menu-item-name">{item.name}</span>
                {item.description && <span className="menu-item-description">{item.description}</span>}
                <span className="menu-item-price">
                  {formatMoney(isTakeaway && item.takeawayPrice ? item.takeawayPrice : item.price)}
                </span>
                {item.allergens.length > 0 && (
                  <span className="menu-item-allergens">
                    {t('menu.containsAllergens')}:{' '}
                    {item.allergens.map((allergen) => t(`menu.allergen.${allergen}`)).join(', ')}
                  </span>
                )}
              </button>
            ))}
          </div>
        </section>
      ))}
    </div>
  );
}
