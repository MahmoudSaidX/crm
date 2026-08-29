import { HttpClient, HttpRequest } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { AppConfigStore } from '../config/app-config.store';
import { validateAppConfig } from '../config/validate-app-config';
import { provideHttpPlatform } from './provide-http-platform';

describe('apiBaseUrlInterceptor', () => {
  let http: HttpClient;
  let controller: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpPlatform(), provideHttpClientTesting()],
    });
    TestBed.inject(AppConfigStore).set(
      validateAppConfig({
        apiBaseUrl: 'https://api.example.test',
        defaultLocale: 'en',
        supportedLocales: ['en', 'ar'],
        appSurface: 'agent-crm',
      }),
    );
    http = TestBed.inject(HttpClient);
    controller = TestBed.inject(HttpTestingController);
  });

  afterEach(() => controller.verify());

  it('prefixes a relative URL with the runtime apiBaseUrl', () => {
    http.get('/agents').subscribe();

    const request = controller.expectOne('https://api.example.test/agents');
    expect(request.request.method).toBe('GET');
    request.flush({});
  });

  it('normalises a relative URL that does not start with a slash', () => {
    http.get('agents').subscribe();

    const request = controller.expectOne('https://api.example.test/agents');
    expect(request.request.url).toBe('https://api.example.test/agents');
    request.flush({});
  });

  it('leaves absolute URLs untouched', () => {
    http.get('https://third-party.example/ping').subscribe();
    http.get('http://third-party.example/ping').subscribe();

    const httpsRequest = controller.expectOne(
      (request: HttpRequest<unknown>) => request.url === 'https://third-party.example/ping',
    );
    const httpRequest = controller.expectOne(
      (request: HttpRequest<unknown>) => request.url === 'http://third-party.example/ping',
    );

    expect(httpsRequest.request.url).toBe('https://third-party.example/ping');
    expect(httpRequest.request.url).toBe('http://third-party.example/ping');
    httpsRequest.flush({});
    httpRequest.flush({});
  });
});
