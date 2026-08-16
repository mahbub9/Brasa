import { formatMoney } from '@brasa/ui/lib/money';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button } from '@brasa/ui/components/Button';
import { Modal, ModalActions } from '@brasa/ui/components/Modal';
import type { MenuItemDto, ModifierGroupDto } from '../api/types';

interface ModifierPickerProps {
  item: MenuItemDto;
  busy: boolean;
  onConfirm: (selectedModifierIds: string[]) => void;
  onCancel: () => void;
}

/**
 * Shown when a tapped menu item has modifier groups (CAT-03/04) — a required
 * single-select group renders as radio-like buttons, everything else as
 * multi-select toggles capped at the group's maxSelect. Validity mirrors
 * exactly what the API itself enforces (each group's min/maxSelect), so a
 * guest never sees "Add" enabled only to have the server reject it.
 */
export function ModifierPicker({ item, busy, onConfirm, onCancel }: ModifierPickerProps) {
  const { t } = useTranslation();
  const [selected, setSelected] = useState<Set<string>>(new Set());

  function countInGroup(group: ModifierGroupDto): number {
    return group.modifiers.filter((m) => selected.has(m.id)).length;
  }

  function toggle(group: ModifierGroupDto, modifierId: string) {
    setSelected((prev) => {
      const next = new Set(prev);
      const groupIds = group.modifiers.map((m) => m.id);

      if (next.has(modifierId)) {
        next.delete(modifierId);
        return next;
      }

      if (group.maxSelect === 1) {
        // Single-select: picking one replaces whatever else was chosen.
        groupIds.forEach((id) => next.delete(id));
        next.add(modifierId);
        return next;
      }

      if (countInGroup(group) >= group.maxSelect) {
        return prev; // already at the cap — ignore the tap
      }

      next.add(modifierId);
      return next;
    });
  }

  const isValid = item.modifierGroups.every((g) => {
    const count = countInGroup(g);
    return count >= g.minSelect && count <= g.maxSelect;
  });

  function selectionHint(group: ModifierGroupDto): string {
    if (group.maxSelect === 1) {
      return t('modifiers.chooseOne');
    }
    if (group.minSelect === 0) {
      return t('modifiers.chooseUpTo', { count: group.maxSelect });
    }
    return t('modifiers.chooseBetween', { min: group.minSelect, max: group.maxSelect });
  }

  return (
    <Modal title={item.name} className="modifier-picker">
      {item.modifierGroups.map((group) => (
        <fieldset key={group.id} className="modifier-group">
          <legend>
            {group.name}
            {group.isRequired && <span className="modifier-required">{t('modifiers.required')}</span>}
            <span className="modifier-hint">{selectionHint(group)}</span>
          </legend>
          <div className="modifier-choices">
            {group.modifiers.map((modifier) => {
              const isSelected = selected.has(modifier.id);
              return (
                <button
                  key={modifier.id}
                  type="button"
                  data-testid={`modifier-${modifier.name}`}
                  className={isSelected ? 'modifier-choice active' : 'modifier-choice'}
                  aria-pressed={isSelected}
                  onClick={() => toggle(group, modifier.id)}
                >
                  <span>{modifier.name}</span>
                  {modifier.priceDelta.amount !== 0 && (
                    <span className="modifier-price">
                      {modifier.priceDelta.amount > 0 ? '+' : ''}
                      {formatMoney(modifier.priceDelta)}
                    </span>
                  )}
                </button>
              );
            })}
          </div>
        </fieldset>
      ))}

      <ModalActions>
        <Button variant="ghost" onClick={onCancel} disabled={busy}>
          {t('modifiers.cancel')}
        </Button>
        <Button data-testid="confirm-modifiers" disabled={busy || !isValid} onClick={() => onConfirm([...selected])}>
          {t('modifiers.add')}
        </Button>
      </ModalActions>
    </Modal>
  );
}
