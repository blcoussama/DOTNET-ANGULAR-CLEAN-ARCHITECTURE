// @ts-check
const eslint = require('@eslint/js');
const { defineConfig } = require('eslint/config');
const tseslint = require('typescript-eslint');
const angular = require('angular-eslint');

/** Règles de sécurité : jeton en mémoire uniquement, aucune injection HTML non maîtrisée. */
const STORAGE_MESSAGE =
  'Interdit : aucune donnée de session ne doit être persistée dans le navigateur (jeton en mémoire uniquement, refresh via cookie httpOnly).';
const securityRules = {
  'no-restricted-globals': [
    'error',
    { name: 'localStorage', message: STORAGE_MESSAGE },
    { name: 'sessionStorage', message: STORAGE_MESSAGE },
    { name: 'indexedDB', message: STORAGE_MESSAGE },
  ],
  'no-restricted-properties': [
    'error',
    { object: 'window', property: 'localStorage', message: STORAGE_MESSAGE },
    { object: 'window', property: 'sessionStorage', message: STORAGE_MESSAGE },
    { object: 'globalThis', property: 'localStorage', message: STORAGE_MESSAGE },
    { object: 'globalThis', property: 'sessionStorage', message: STORAGE_MESSAGE },
    { object: 'document', property: 'cookie', message: 'Les cookies d’authentification sont httpOnly : ne pas lire ni écrire document.cookie.' },
    { object: 'document', property: 'write', message: 'document.write est interdit.' },
  ],
  'no-restricted-syntax': [
    'error',
    {
      selector: "MemberExpression[property.name=/^(localStorage|sessionStorage)$/]",
      message: STORAGE_MESSAGE,
    },
    {
      selector: "MemberExpression[property.name=/^bypassSecurityTrust/]",
      message: 'Interdit : ne jamais contourner l’assainissement d’Angular (DomSanitizer.bypassSecurityTrust*).',
    },
    {
      selector: "AssignmentExpression > MemberExpression[property.name=/^(innerHTML|outerHTML)$/]",
      message: 'Interdit : pas d’injection HTML ; utiliser l’interpolation Angular.',
    },
    {
      selector: "CallExpression[callee.property.name='insertAdjacentHTML']",
      message: 'Interdit : pas d’injection HTML ; utiliser l’interpolation Angular.',
    },
  ],
  'no-eval': 'error',
  'no-implied-eval': 'error',
  'no-new-func': 'error',
};

module.exports = defineConfig([
  {
    ignores: ['dist/**', '.angular/**', 'node_modules/**', 'coverage/**'],
  },
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
      ...securityRules,
      '@angular-eslint/directive-selector': [
        'error',
        {
          type: 'attribute',
          prefix: 'app',
          style: 'camelCase',
        },
      ],
      '@angular-eslint/component-selector': [
        'error',
        {
          type: 'element',
          prefix: 'app',
          style: 'kebab-case',
        },
      ],
      '@angular-eslint/prefer-on-push-component-change-detection': 'error',
      '@typescript-eslint/no-unused-vars': ['error', { argsIgnorePattern: '^_', varsIgnorePattern: '^_' }],
      '@typescript-eslint/no-explicit-any': 'error',
    },
  },
  {
    files: ['**/*.spec.ts'],
    rules: {
      '@typescript-eslint/no-non-null-assertion': 'off',
    },
  },
  {
    files: ['**/*.html'],
    extends: [angular.configs.templateRecommended, angular.configs.templateAccessibility],
    rules: {
      '@angular-eslint/template/prefer-control-flow': 'error',
      '@angular-eslint/template/no-any': 'error',
      '@angular-eslint/template/button-has-type': 'error',
    },
  },
]);
