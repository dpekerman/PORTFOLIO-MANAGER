/**
 * Conservative ticker clean-up for symbols handed to the Add Stock dialog from another screen: trims, upper-cases and
 * collapses an accidentally repeated exchange suffix (`ABC.TO.TO` -> `ABC.TO`). It never adds, removes or swaps an
 * exchange, so provider-specific formats such as `BRK-B` or `RCI-B.TO` pass through untouched.
 */
export function normalizePrefillSymbol(raw: string | null | undefined): string {
  const symbol = (raw ?? '').trim().toUpperCase();
  return symbol.replace(/(\.[A-Z]{1,3})\1+$/, '$1');
}
