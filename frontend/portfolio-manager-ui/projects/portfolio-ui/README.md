# portfolio-ui

Internal, application-agnostic UI primitives and design tokens for Portfolio Manager Angular applications.

## Ownership

`portfolio-ui` owns stateless presentation patterns:

- Page and section headers
- Empty and loading states
- Semantic status badges
- Dialog layout
- Shared theme variables

The consuming application owns routing, API/state services, financial models, demo masking, authentication, and domain-specific components. Angular Material remains the interaction and accessibility layer for buttons, forms, tables, dialogs, and overlays.

## Usage

Build the library before consuming it through the generated workspace path mapping:

```powershell
npm run build:ui
```

Import components from the public entry point:

```typescript
import { EmptyState, PageHeader, StatusBadge } from 'portfolio-ui';
```

The application imports `projects/portfolio-ui/src/styles/_theme.scss` while developing in this workspace. Packaged consumers can import the same asset through the `portfolio-ui/theme` Sass export.

## Styling Rules

- Tailwind v4 is compiled by the consuming application, not by this library.
- Use complete, statically detectable Tailwind class names.
- Use semantic theme variables instead of hardcoded feature colors.
- Use Tailwind utilities for layout, spacing, typography, and responsive behavior.
- Keep data-driven dimensions and specialized visualization styles in the owning component SCSS.
- Customize Angular Material through supported Material 3 tokens and mixins.

## Validation

```powershell
npm run build:ui
npm run test:ui
```
