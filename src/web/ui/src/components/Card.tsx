import type { HTMLAttributes } from 'react';

/** The one card/panel affordance shared by the floor plan, menu grid and admin lists (WEB-02). */
export function Card({ className, ...rest }: HTMLAttributes<HTMLDivElement>) {
  const classes = className ? `brasa-card ${className}` : 'brasa-card';
  return <div className={classes} {...rest} />;
}
