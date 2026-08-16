import { useId } from 'react';
import type { InputHTMLAttributes } from 'react';

interface TextFieldProps extends InputHTMLAttributes<HTMLInputElement> {
  /** Omit for a bare, aria-labelled control that drops into an existing compact row (e.g. a table's inline edit form). */
  label?: string;
}

/**
 * Labeled text input (WEB-02) — 44px tall, brand-colored focus ring,
 * replacing each app's own unstyled `<input>`. Renders just the styled
 * control with no wrapper when `label` is omitted, so it still fits inline
 * into a compact row that already supplies its own `aria-label` and layout
 * (e.g. FloorManager's table edit row).
 */
export function TextField({ label, id, className, ...rest }: TextFieldProps) {
  const generatedId = useId();
  const inputId = id ?? generatedId;

  if (!label) {
    const classes = ['brasa-field-control', className].filter(Boolean).join(' ');
    return <input id={id} className={classes} {...rest} />;
  }

  return (
    <div className="brasa-field">
      <label htmlFor={inputId} className="brasa-field-label">
        {label}
      </label>
      <input id={inputId} className={['brasa-field-control', className].filter(Boolean).join(' ')} {...rest} />
    </div>
  );
}
