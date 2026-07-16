# Al-Falah UI Enhancement

This package contains the enhanced `src` folder for the Angular application.

## What changed

- Preserved the existing Al-Falah green-and-gold identity.
- Reduced the global font scale, page spacing, card padding, controls, table rows, and action buttons.
- Rebuilt the application shell with a slimmer topbar, compact sidebar, clearer active states, and responsive collapsed navigation.
- Rebuilt both login screens with consistent branding, compact forms, improved focus states, and restrained motion.
- Split the former monolithic `styles.css` into focused production-style modules:
  - `styles/design-tokens.css`
  - `styles/base.css`
  - `styles/layout.css`
  - `styles/primeng.css`
  - `styles/motion.css`
  - `styles/responsive.css`
- Unified PrimeNG inputs, dropdowns, buttons, dialogs, tables, tags, paginator, tooltips, and action groups.
- Tightened dashboard metrics, charts, maps, forms, details, empty states, and report layouts.
- Added reduced-motion and print behavior.

## Installation

Extract this ZIP into the project root and allow the included `src` folder to merge with or replace the current `src` folder. Keep a backup of the existing source first.

## Validation performed

- All original source files are present.
- CSS parser check: passed with zero syntax errors.
- TypeScript syntax transpilation check: passed with zero syntax errors.
- Translation JSON validation: passed.

A full Angular production build was not possible because the uploaded archive contained only the `src` folder and did not include `package.json`, `angular.json`, or installed dependencies.
