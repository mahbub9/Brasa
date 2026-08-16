import type { HTMLAttributes } from 'react';

export type BadgeTone = 'success' | 'brand' | 'warning' | 'neutral' | 'danger';

interface BadgeProps extends HTMLAttributes<HTMLSpanElement> {
  tone: BadgeTone;
}

/**
 * One shape for every state that used to read as plain text — table state,
 * item availability, order status (WEB-02). Callers map their own domain
 * state to a tone; this component knows nothing about tables or orders.
 */
export function Badge({ tone, className, ...rest }: BadgeProps) {
  const classes = ['brasa-badge', `brasa-badge-${tone}`, className].filter(Boolean).join(' ');
  return <span className={classes} {...rest} />;
}
