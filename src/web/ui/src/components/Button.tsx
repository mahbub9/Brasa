import type { ButtonHTMLAttributes } from 'react';

export type ButtonVariant = 'primary' | 'secondary' | 'ghost' | 'danger';

interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: ButtonVariant;
}

/**
 * The beta UI refresh's one button (WEB-02) — every variant is at least
 * `--touch-target` (44px) tall with real hover/focus-visible/disabled
 * states, replacing each app's own hand-rolled, ~30px-tall buttons.
 * `type="button"` by default: every existing call site is inside a form-less
 * click handler, and a stray implicit submit is the more common bug.
 */
export function Button({ variant = 'primary', type = 'button', className, ...rest }: ButtonProps) {
  const classes = ['brasa-btn', `brasa-btn-${variant}`, className].filter(Boolean).join(' ');
  return <button type={type} className={classes} {...rest} />;
}
