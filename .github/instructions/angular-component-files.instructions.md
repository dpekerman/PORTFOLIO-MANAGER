---
applyTo: "**/*.component.ts,**/*.directive.ts"
---

# Angular Component File Structure Rules

## MANDATORY: Always Use Separate Template and Style Files

Every Angular component and directive **MUST** use external files for templates and styles. **Never** use inline `template`, `styles`, or `styleUrls` array with inline content.

### Required pattern for every component:

```typescript
@Component({
  selector: "app-my-component",
  templateUrl: "./my-component.component.html", // ← always external file
  styleUrl: "./my-component.component.scss", // ← always external file
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MyComponent {}
```

### Rules

- **NEVER use `template:`** — always use `templateUrl:`
- **NEVER use `styles:`** — always use `styleUrl:` (singular, Angular v17+)
- The template file must be named `<component-name>.component.html`
- The style file must be named `<component-name>.component.scss`
- Both files must live in the **same directory** as the component `.ts` file
- Inline style attributes on elements (e.g. `style="margin-left: 8px"`) should be moved to the `.scss` file

### File grouping per component

Each component directory must contain these files together:

```
feature-name/
├── feature-name.component.ts       ← class only, no template/styles inline
├── feature-name.component.html     ← all template markup
├── feature-name.component.scss     ← all component styles
└── feature-name.routes.ts
```

### Why

- Templates and styles grow large — keeping them inline makes `.ts` files unreadable
- IDE support (syntax highlighting, formatting, IntelliSense) works best in dedicated files
- Reviewers can review markup and styles independently from logic
- Satisfies the Angular Style Guide rule [Style 05-04](https://angular.dev/style-guide#style-05-04)
