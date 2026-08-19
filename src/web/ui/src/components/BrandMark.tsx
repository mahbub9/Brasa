/**
 * The header's brand mark — three embers of ascending height ("brasa" is
 * Portuguese for hot coals/embers, so this is a literal mark, not an
 * arbitrary shape). Plain rounded rects rather than a hand-drawn flame
 * path: simple enough to render correctly at 26px with zero curve-math
 * risk. Shared by pos and admin so the header lockup is identical, not
 * two hand-copied SVGs.
 */
export function BrandMark() {
  return (
    <svg viewBox="0 0 24 24" className="brasa-mark" aria-hidden="true">
      <rect x="3" y="12" width="5" height="9" rx="2.5" />
      <rect x="9.5" y="6" width="5" height="15" rx="2.5" />
      <rect x="16" y="9" width="5" height="12" rx="2.5" />
    </svg>
  );
}
