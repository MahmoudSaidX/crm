import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { AppConfigStore } from '../config/app-config.store';

const ABSOLUTE_URL = /^https?:\/\//i;

/**
 * Prefixes relative API request URLs with the runtime `apiBaseUrl`.
 *
 * Absolute URLs pass through untouched so that requests to third parties (or to an
 * explicitly-addressed host) are never rewritten. Authentication headers, retries and
 * token refresh are deliberately NOT here — CRM-110 and capability stories own those.
 */
export const apiBaseUrlInterceptor: HttpInterceptorFn = (request, next) => {
  if (ABSOLUTE_URL.test(request.url)) {
    return next(request);
  }

  const { apiBaseUrl } = inject(AppConfigStore).require();
  const path = request.url.startsWith('/') ? request.url : `/${request.url}`;

  return next(request.clone({ url: `${apiBaseUrl}${path}` }));
};
