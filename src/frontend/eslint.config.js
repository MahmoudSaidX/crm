// @ts-check
const eslint = require('@eslint/js');
const { defineConfig } = require('eslint/config');
const tseslint = require('typescript-eslint');
const angular = require('angular-eslint');
const prettier = require('eslint-config-prettier');

/**
 * Broad UI libraries that would compete with PrimeNG. ADR-009 makes PrimeNG the
 * primary component library; adding one of these requires a new ADR, not an import.
 */
const FORBIDDEN_UI_LIBRARIES = [
  '@angular/material',
  '@angular/material/*',
  '@angular/cdk',
  '@angular/cdk/*',
  '@mui/*',
  'bootstrap',
  'bootstrap/*',
  '@ionic/angular',
  '@ionic/angular/*',
  'ng-zorro-antd',
  'ng-zorro-antd/*',
];

const APPLICATION_INTERNALS = [
  'projects/agent-crm/*',
  'projects/customer-portal/*',
  '**/agent-crm/src/**',
  '**/customer-portal/src/**',
];

/**
 * Builds a `no-restricted-imports` rule from the dependency-boundary table in
 * `src/frontend/README.md`.
 */
function restrictedImports(extraPatterns = []) {
  return [
    'error',
    {
      patterns: [
        {
          group: FORBIDDEN_UI_LIBRARIES,
          message:
            'Angular Material and other broad UI libraries are forbidden by ADR-009. PrimeNG is the primary component library.',
        },
        ...extraPatterns,
      ],
    },
  ];
}

module.exports = defineConfig([
  {
    files: ['**/*.ts'],
    extends: [
      eslint.configs.recommended,
      tseslint.configs.recommended,
      tseslint.configs.stylistic,
      angular.configs.tsRecommended,
    ],
    processor: angular.processInlineTemplates,
    rules: {
      'no-restricted-imports': restrictedImports(),
    },
  },

  // --- Dependency boundaries -------------------------------------------------
  // @squad-crm/platform: framework/runtime foundation. Presentation-free, and it
  // must not reach sideways into shared-ui or down into an application.
  {
    files: ['projects/platform/**/*.ts'],
    rules: {
      'no-restricted-imports': restrictedImports([
        {
          group: ['@squad-crm/shared-ui', ...APPLICATION_INTERNALS],
          message:
            '@squad-crm/platform may not depend on @squad-crm/shared-ui or on any application.',
        },
        {
          group: ['primeng', 'primeng/*', '@primeng/*'],
          message:
            '@squad-crm/platform is presentation-free: keep PrimeNG in @squad-crm/shared-ui.',
        },
      ]),
    },
  },
  // @squad-crm/shared-ui: presentation only. platform and shared-ui are siblings —
  // neither may depend on the other, and neither may depend on an application.
  {
    files: ['projects/shared-ui/**/*.ts'],
    rules: {
      'no-restricted-imports': restrictedImports([
        {
          group: ['@squad-crm/platform', ...APPLICATION_INTERNALS],
          message:
            '@squad-crm/shared-ui may not depend on @squad-crm/platform or on any application.',
        },
      ]),
    },
  },
  // Applications may use both shared libraries, but never each other's internals.
  {
    files: ['projects/agent-crm/**/*.ts'],
    rules: {
      'no-restricted-imports': restrictedImports([
        {
          group: ['projects/customer-portal/*', '**/customer-portal/src/**'],
          message: "Applications cannot import each other's internals.",
        },
      ]),
    },
  },
  {
    files: ['projects/customer-portal/**/*.ts'],
    rules: {
      'no-restricted-imports': restrictedImports([
        {
          group: ['projects/agent-crm/*', '**/agent-crm/src/**'],
          message: "Applications cannot import each other's internals.",
        },
      ]),
    },
  },

  // --- Component/directive selector prefixes per project ----------------------
  ...[
    { path: 'agent-crm', prefix: 'crm' },
    { path: 'customer-portal', prefix: 'portal' },
    { path: 'platform', prefix: 'sc' },
    { path: 'shared-ui', prefix: 'sc' },
  ].map(({ path, prefix }) => ({
    files: [`projects/${path}/**/*.ts`],
    rules: {
      '@angular-eslint/component-selector': [
        'error',
        { type: 'element', prefix, style: 'kebab-case' },
      ],
      '@angular-eslint/directive-selector': [
        'error',
        { type: 'attribute', prefix, style: 'camelCase' },
      ],
    },
  })),

  {
    files: ['**/*.html'],
    extends: [angular.configs.templateRecommended, angular.configs.templateAccessibility],
    rules: {},
  },

  // Must stay last: turns off stylistic rules that Prettier owns.
  prettier,
]);
