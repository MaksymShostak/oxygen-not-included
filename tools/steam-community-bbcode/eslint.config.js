// SPDX-License-Identifier: AGPL-3.0-only
import {defineConfig} from 'eslint/config';
import js from '@eslint/js';
import globals from 'globals';

export default defineConfig([{
  files: ['src/**/*.js', 'scripts/**/*.js', 'test/**/*.js', 'eslint.config.js'],
  extends: [js.configs.recommended],
  languageOptions: {sourceType: 'module', globals: globals.nodeBuiltin},
  linterOptions: {noInlineConfig: true},
  rules: {'no-unused-vars': ['error', {ignoreRestSiblings: true}]},
}]);
