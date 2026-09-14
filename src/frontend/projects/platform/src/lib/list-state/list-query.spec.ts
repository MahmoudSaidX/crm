import { convertToParamMap } from '@angular/router';
import {
  DEFAULT_PAGE,
  DEFAULT_PAGE_SIZE,
  MAX_PAGE_SIZE,
  readEnum,
  readFlag,
  readGuid,
  readPage,
  readPageSize,
  readText,
  toQueryParams,
} from './list-query';

const map = (params: Record<string, string>) => convertToParamMap(params);

describe('list-query', () => {
  describe('readPage', () => {
    it('reads a valid page', () => {
      expect(readPage(map({ page: '3' }))).toBe(3);
    });

    // A hand-edited or stale URL must render a working screen, not an error.
    for (const raw of ['0', '-1', 'abc', '2.5', '', ' ', '1e3', '0x10', '12abc', '9'.repeat(20)]) {
      it(`falls back to page ${DEFAULT_PAGE} for ${JSON.stringify(raw)}`, () => {
        expect(readPage(map({ page: raw }))).toBe(DEFAULT_PAGE);
      });
    }

    it('falls back when the parameter is absent', () => {
      expect(readPage(map({}))).toBe(DEFAULT_PAGE);
    });
  });

  describe('readPageSize', () => {
    it('reads a valid page size', () => {
      expect(readPageSize(map({ pageSize: '50' }))).toBe(50);
    });

    // Clamped rather than rejected: the screen still works, and the request
    // stays inside the bound the backend independently enforces.
    it('clamps an oversized page size to the backend maximum', () => {
      expect(readPageSize(map({ pageSize: '100000' }))).toBe(MAX_PAGE_SIZE);
    });

    for (const raw of ['0', '-5', 'all', '']) {
      it(`falls back to ${DEFAULT_PAGE_SIZE} for ${JSON.stringify(raw)}`, () => {
        expect(readPageSize(map({ pageSize: raw }))).toBe(DEFAULT_PAGE_SIZE);
      });
    }
  });

  describe('readText', () => {
    it('trims surrounding whitespace', () => {
      expect(readText(map({ search: '  payment  ' }), 'search')).toBe('payment');
    });

    it('truncates at the bound', () => {
      expect(readText(map({ search: 'a'.repeat(500) }), 'search').length).toBe(200);
    });

    // The point of the whole design: search text is DATA. Nothing here may
    // strip, escape or "clean" it — doing so would corrupt real names and
    // would protect nothing, because the protection is parameterized queries
    // on the server and interpolation in the template.
    for (const term of [
      "O'Brien",
      "'; DROP TABLE customers; --",
      '<script>alert(1)</script>',
      'محمود',
      'عبد الله',
      'Ünicode ✨ 名前',
      '100% & <b>bold</b>',
    ]) {
      it(`passes ${JSON.stringify(term)} through unchanged`, () => {
        expect(readText(map({ search: term }), 'search')).toBe(term);
      });
    }

    it('returns an empty string when absent', () => {
      expect(readText(map({}), 'search')).toBe('');
    });
  });

  describe('readEnum', () => {
    const allowed = ['Open', 'Completed'] as const;

    it('accepts an allow-listed value', () => {
      expect(readEnum(map({ status: 'Open' }), 'status', allowed)).toBe('Open');
    });

    // The allow-list is the sort/filter protection: an unknown value is
    // dropped here and therefore never reaches the API at all.
    for (const raw of ['open', 'Deleted', 'DROP TABLE', '', 'Open;--']) {
      it(`rejects ${JSON.stringify(raw)}`, () => {
        expect(readEnum(map({ status: raw }), 'status', allowed)).toBeNull();
      });
    }
  });

  describe('readGuid', () => {
    it('accepts a well-formed id', () => {
      const id = '2f1a4c9e-7b3d-4a6f-8e12-0b9d5c3a7e41';
      expect(readGuid(map({ categoryId: id }), 'categoryId')).toBe(id);
    });

    for (const raw of ['not-a-guid', '123', "' OR 1=1 --", '']) {
      it(`rejects ${JSON.stringify(raw)}`, () => {
        expect(readGuid(map({ categoryId: raw }), 'categoryId')).toBeNull();
      });
    }
  });

  describe('readFlag', () => {
    it('is true only for the exact string true', () => {
      expect(readFlag(map({ mine: 'true' }), 'mine')).toBeTrue();
      expect(readFlag(map({ mine: 'TRUE' }), 'mine')).toBeFalse();
      expect(readFlag(map({ mine: '1' }), 'mine')).toBeFalse();
      expect(readFlag(map({}), 'mine')).toBeFalse();
    });
  });

  describe('toQueryParams', () => {
    // null is how Angular's router removes a parameter, so "omitted" and
    // "cleared" are the same operation.
    it('omits defaults, empties and false', () => {
      expect(
        toQueryParams(
          { page: 1, search: '', status: null, mine: false, sort: 'Name' },
          { page: 1, sort: 'Name' },
        ),
      ).toEqual({ page: null, search: null, status: null, mine: null, sort: null });
    });

    it('keeps non-default values as strings', () => {
      expect(toQueryParams({ page: 3, search: 'محمود' }, { page: 1 })).toEqual({
        page: '3',
        search: 'محمود',
      });
    });
  });
});
