import { useId } from 'react';
import type { SelectHTMLAttributes } from 'react';

interface SelectFieldProps extends SelectHTMLAttributes<HTMLSelectElement> {
  /** Omit for a bare, aria-labelled control that drops into an existing compact row. */
  label?: string;
}

/**
 * Labeled select (WEB-02) — same visual family as {@link TextField}: 44px
 * tall, brand-colored focus ring, a custom chevron in place of the
 * platform default so it lines up with the rest of the system.
 */
export function SelectField({ label, id, className, children, ...rest }: SelectFieldProps) {
  const generatedId = useId();
  const selectId = id ?? generatedId;

  if (!label) {
    const classes = ['brasa-field-control', className].filter(Boolean).join(' ');
    return (
      <select id={id} className={classes} {...rest}>
        {children}
      </select>
    );
  }

  return (
    <div className="brasa-field">
      <label htmlFor={selectId} className="brasa-field-label">
        {label}
      </label>
      <select id={selectId} className={['brasa-field-control', className].filter(Boolean).join(' ')} {...rest}>
        {children}
      </select>
    </div>
  );
}
