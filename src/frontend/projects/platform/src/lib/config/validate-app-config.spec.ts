import { AppConfigError, validateAppConfig } from './validate-app-config';

describe('validateAppConfig', () => {
  const valid = {
    apiBaseUrl: 'http://localhost:5080',
    defaultLocale: 'en',
    supportedLocales: ['en', 'ar'],
    appSurface: 'agent-crm',
  };

  it('accepts a well-formed configuration and trims a trailing slash', () => {
    const config = validateAppConfig({ ...valid, apiBaseUrl: 'http://localhost:5080/' });

    expect(config.apiBaseUrl).toBe('http://localhost:5080');
    expect(config.defaultLocale).toBe('en');
    expect(config.supportedLocales).toEqual(['en', 'ar']);
    expect(config.appSurface).toBe('agent-crm');
  });

  it('throws when apiBaseUrl is missing or empty', () => {
    expect(() => validateAppConfig({ ...valid, apiBaseUrl: '' })).toThrowError(AppConfigError);
    expect(() => validateAppConfig({ ...valid, apiBaseUrl: undefined })).toThrowError(
      AppConfigError,
    );
  });

  it('throws when apiBaseUrl is still a deployment placeholder', () => {
    expect(() => validateAppConfig({ ...valid, apiBaseUrl: 'REPLACE_ME' })).toThrowError(
      AppConfigError,
    );
  });

  it('rejects unsupported locales', () => {
    expect(() => validateAppConfig({ ...valid, defaultLocale: 'fr' })).toThrowError(AppConfigError);
    expect(() => validateAppConfig({ ...valid, supportedLocales: ['en', 'fr'] })).toThrowError(
      AppConfigError,
    );
  });

  it('rejects a defaultLocale that is not in supportedLocales', () => {
    expect(() =>
      validateAppConfig({ ...valid, supportedLocales: ['en'], defaultLocale: 'ar' }),
    ).toThrowError(AppConfigError);
  });

  it('rejects an unknown appSurface', () => {
    expect(() => validateAppConfig({ ...valid, appSurface: 'admin' })).toThrowError(AppConfigError);
  });
});
