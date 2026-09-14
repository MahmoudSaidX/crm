import { Signal, inject } from '@angular/core';
import { ActivatedRoute, ParamMap, Params, Router } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { distinctUntilChanged, map } from 'rxjs';

/** Every list state carries a page and a page size; the rest is screen-specific. */
export interface PagedListState {
  readonly page: number;
  readonly pageSize: number;
}

export interface ListUrlState<TState extends PagedListState> {
  /**
   * The current list state, parsed and normalized from the URL. Re-emits only
   * when the query string actually changes, so an effect that loads from it
   * issues exactly one request per real state change — including on
   * Back/Forward, which is an ordinary navigation like any other.
   */
  readonly state: Signal<TState>;

  /**
   * Applies a state change that alters *what* is listed — a search term, a
   * filter — and therefore resets to page 1. Paging to 3, then narrowing the
   * search, must not leave the user on a page-3 window of a two-page result.
   */
  patch(changes: Partial<TState>): void;

  /** Moves within the current result set, preserving search and filters. */
  setPage(page: number): void;
}

/**
 * Makes the URL the single source of truth for a list screen.
 *
 * The data flow is deliberately one-way and has exactly one cycle:
 *
 * ```
 * URL query params → parse → validate/normalize → state → API request
 * UI change        → router.navigate(...)       → (the line above)
 * ```
 *
 * A component using this never calls its own `load()` from a click handler and
 * never keeps list state that the URL does not carry. That is what makes
 * refresh, deep links and browser Back/Forward work without any extra code:
 * they are all just "the URL changed". It is also why component state cannot
 * drift out of sync with the address bar — there is no second copy to drift.
 *
 * @param parse Reads normalized state out of the query params. Must not throw:
 *   the URL is untrusted, so unusable values fall back to defaults.
 * @param toParams Renders state back to query params, with defaults mapped to
 *   `null` so they disappear from the URL.
 */
export function injectListUrlState<TState extends PagedListState>(
  parse: (params: ParamMap) => TState,
  toParams: (state: TState) => Params,
): ListUrlState<TState> {
  const route = inject(ActivatedRoute);
  const router = inject(Router);

  const state = toSignal(
    route.queryParamMap.pipe(
      // The router re-emits on any navigation to this route, including one that
      // changed nothing. Comparing the serialized parameters is what stops a
      // redundant navigation from firing a second, identical API request.
      distinctUntilChanged((a, b) => serialize(a) === serialize(b)),
      map(parse),
    ),
    { requireSync: true },
  );

  const apply = (next: TState): void => {
    void router.navigate([], {
      relativeTo: route,
      queryParams: toParams(next),
      // `merge` + null values: parameters this screen owns are replaced or
      // removed, and anything else already on the URL is left alone.
      queryParamsHandling: 'merge',
    });
  };

  return {
    state,
    patch: (changes) => apply({ ...state(), ...changes, page: 1 }),
    setPage: (page) => apply({ ...state(), page }),
  };
}

function serialize(params: ParamMap): string {
  return [...params.keys]
    .sort()
    .map((key) => `${key}=${params.getAll(key).join(',')}`)
    .join('&');
}
