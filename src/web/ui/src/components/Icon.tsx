const PATHS = {
  back: 'M15 18l-6-6 6-6',
  close: 'M6 6l12 12M18 6L6 18',
  add: 'M12 5v14M5 12h14',
  edit: 'M4 20l4-1 11-11-3-3L5 16l-1 4z',
  delete: 'M4 7h16M9 7V5a1 1 0 011-1h4a1 1 0 011 1v2m2 0-1 13a1 1 0 01-1 1H8a1 1 0 01-1-1L6 7',
  confirm: 'M4 12l5 5L20 6',
} as const;

const SEARCH_CIRCLE = { cx: 11, cy: 11, r: 6 } as const;
const SEARCH_HANDLE = 'M20 20l-4-4';

export type IconName = keyof typeof PATHS | 'search';

interface IconProps {
  name: IconName;
  className?: string;
}

/**
 * The beta UI refresh's icon set (WEB-02) — seven inline SVGs covering the
 * actions that recur constantly across pos/admin/kds. Deliberately no
 * icon-font/library dependency, consistent with how self-contained the
 * rest of this codebase already is; stroke color inherits `currentColor`
 * so an icon always matches the text color around it.
 */
export function Icon({ name, className }: IconProps) {
  const classes = className ? `brasa-icon ${className}` : 'brasa-icon';

  if (name === 'search') {
    return (
      <svg viewBox="0 0 24 24" className={classes} aria-hidden="true">
        <circle cx={SEARCH_CIRCLE.cx} cy={SEARCH_CIRCLE.cy} r={SEARCH_CIRCLE.r} />
        <path d={SEARCH_HANDLE} />
      </svg>
    );
  }

  return (
    <svg viewBox="0 0 24 24" className={classes} aria-hidden="true">
      <path d={PATHS[name]} />
    </svg>
  );
}
