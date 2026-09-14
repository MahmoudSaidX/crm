import { ParamMap, Params } from '@angular/router';

/**
 * Parsing and normalization for list state carried in the URL query string.
 *
 * Query parameters are untrusted input: anybody can type them, and a stale
 * bookmark can carry a value that no longer means anything. Every reader here
 * therefore *normalizes rather than throws* — an unusable value falls back to
 * the default so a hand-edited URL renders a working screen instead of an error.
 * The backend re-validates all of it and is the actual security boundary; this
 * is UX defence, and the two are not interchangeable.
 *
 * What these helpers deliberately do **not** do is filter characters. No
 * apostrophe is stripped, no angle bracket escaped, no Arabic or emoji dropped.
 * Search text is data: it round-trips to the server as an `HttpParams` value and
 * renders through Angular interpolation, and "cleaning" it would corrupt
 * legitimate business input (`O'Brien`, `محمود`) while protecting nothing.
 */

/** Matches the backend's `PaginationRequest` bounds exactly. */
export const DEFAULT_PAGE = 1;
export const DEFAULT_PAGE_SIZE = 20;
export const MAX_PAGE_SIZE = 200;

/** Longest text the backend accepts for a search or text filter. */
export const MAX_TEXT_FILTER_LENGTH = 200;

const GUID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

/**
 * Reads a positive integer in plain decimal, clamped to `[1, max]`.
 *
 * Deliberately stricter than `Number()`: that would accept `1e3` as 1000 and
 * `0x10` as 16, so the URL and the state it produces would disagree about how
 * the page is written. It is also stricter than `parseInt`, which reads
 * `12abc` as 12 and would let a corrupt URL through as valid. A page number
 * is digits; anything else is a typo and falls back to the default.
 */
const DECIMAL_PATTERN = /^\d+$/;

function readBoundedInteger(raw: string | null, fallback: number, max: number): number {
  const trimmed = raw?.trim();
  if (!trimmed || !DECIMAL_PATTERN.test(trimmed)) {
    return fallback;
  }
  const value = Number(trimmed);
  if (!Number.isSafeInteger(value) || value < 1) {
    return fallback;
  }
  return Math.min(value, max);
}

/** 1-based page number; anything unparseable, zero or negative falls back to 1. */
export function readPage(params: ParamMap): number {
  return readBoundedInteger(params.get('page'), DEFAULT_PAGE, Number.MAX_SAFE_INTEGER);
}

/** Page size, clamped to the backend's maximum so a deep link cannot request the world. */
export function readPageSize(params: ParamMap): number {
  return readBoundedInteger(params.get('pageSize'), DEFAULT_PAGE_SIZE, MAX_PAGE_SIZE);
}

/**
 * Free text, trimmed and bounded — never character-filtered.
 *
 * Truncation (rather than rejection) is right here because the value is only a
 * search term: an over-long one is meaningless, not hostile, and the backend
 * rejects anything past the bound anyway.
 */
export function readText(
  params: ParamMap,
  key: string,
  maxLength = MAX_TEXT_FILTER_LENGTH,
): string {
  const raw = params.get(key);
  if (raw === null) {
    return '';
  }
  return raw.trim().slice(0, maxLength);
}

/** One of `allowed`, or null. The allow-list is the whole point: unknown input never reaches the API. */
export function readEnum<T extends string>(
  params: ParamMap,
  key: string,
  allowed: readonly T[],
): T | null {
  const raw = params.get(key);
  return raw !== null && (allowed as readonly string[]).includes(raw) ? (raw as T) : null;
}

/** A syntactically valid GUID, or null. Existence and authorization are the backend's call. */
export function readGuid(params: ParamMap, key: string): string | null {
  const raw = params.get(key);
  return raw !== null && GUID_PATTERN.test(raw) ? raw : null;
}

/** `true` only for the exact string `true`, so a stray value never enables a filter. */
export function readFlag(params: ParamMap, key: string): boolean {
  return params.get(key) === 'true';
}

/**
 * Builds the query params for a URL, dropping everything at its default.
 *
 * Omitting defaults is what keeps a shared link readable
 * (`/tickets?search=payment` rather than `/tickets?page=1&pageSize=20&search=payment&sort=&dir=`)
 * and, because `null` removes a parameter in Angular's router, it is also how a
 * cleared filter disappears from the URL instead of lingering as `&status=`.
 */
export function toQueryParams(
  state: Readonly<Record<string, string | number | boolean | null>>,
  defaults: Readonly<Record<string, string | number | boolean | null>> = {},
): Params {
  const params: Params = {};
  for (const [key, value] of Object.entries(state)) {
    const isEmpty = value === null || value === '' || value === false;
    const isDefault = key in defaults && value === defaults[key];
    params[key] = isEmpty || isDefault ? null : String(value);
  }
  return params;
}

/**
 * The page/pageSize half of any list state, read from the URL.
 *
 * Every list screen shares these two, so they are parsed in one place rather
 * than repeated eleven times with eleven chances to get a bound wrong.
 */
export function readPagination(params: ParamMap): { page: number; pageSize: number } {
  return { page: readPage(params), pageSize: readPageSize(params) };
}

/** The page/pageSize half of the URL, with defaults omitted. */
export function paginationParams(state: { page: number; pageSize: number }): Params {
  return toQueryParams(
    { page: state.page, pageSize: state.pageSize },
    { page: DEFAULT_PAGE, pageSize: DEFAULT_PAGE_SIZE },
  );
}
