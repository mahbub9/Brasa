import type { HTMLAttributes, ReactNode } from 'react';

interface ModalProps extends HTMLAttributes<HTMLDivElement> {
  title: string;
  children: ReactNode;
}

/**
 * Formalizes the backdrop/panel pattern `pos`'s staff sign-in already used
 * (WEB-02) — same structure (`role="dialog"`, `aria-modal`, the title
 * doubling as the dialog's accessible name), just styled through the
 * shared tokens instead of app-local CSS. Deliberately does not add
 * close-on-backdrop-click or Escape-to-close: that would be new behavior,
 * not a restyle, and every existing modal already has its own explicit
 * cancel button. `...rest` (e.g. `data-testid`) forwards to the panel, the
 * same element existing tests already target.
 */
export function Modal({ title, children, className, ...rest }: ModalProps) {
  const classes = ['brasa-modal', className].filter(Boolean).join(' ');
  return (
    <div className="brasa-modal-backdrop" role="dialog" aria-modal="true" aria-label={title}>
      <div className={classes} {...rest}>
        <h2 className="brasa-modal-title">{title}</h2>
        {children}
      </div>
    </div>
  );
}

/** The right-aligned action-button row at the bottom of a {@link Modal}. */
export function ModalActions({ children }: { children: ReactNode }) {
  return <div className="brasa-modal-actions">{children}</div>;
}
