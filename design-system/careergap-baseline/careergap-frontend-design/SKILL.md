---
name: careergap-frontend-design
description: Design system skill for careergap-frontend. Activate when building UI components, pages, or any visual elements. Provides exact color tokens, typography scale, spacing grid, component patterns, and craft rules. Read references/DESIGN.md before writing any CSS or JSX.
---

# careergap-frontend Design System

You are building UI for **careergap-frontend**. Light-themed, neutral palette, sans-serif typography (inherit), compact density on a 4px grid.

## Design Philosophy

- **Layered depth** — use shadow tokens to create a sense of physical layering. Each elevation level has a specific shadow.
- **Solid colors only** — no gradients anywhere. Every surface is a single flat color.
- **Single typeface** — inherit carries all text. Hierarchy comes from size, weight, and color — never font mixing.
- **compact density** — 4px base grid. Every dimension is a multiple of 4.
- **neutral palette** — the color temperature runs neutral, matching the sans-serif typography.
- **Restrained accent** — `#c9dfcf` is the only pop of color. Used exclusively for CTAs, links, focus rings, and active states.
- **Subtle motion** — transitions smooth state changes. Keep durations under 300ms, use ease-out curves.
- **Phosphor icons** — use Phosphor for all iconography. Do not mix icon libraries.

## Color System

### Core Palette

| Role | Token | Hex | Use |
|------|-------|-----|-----|
| Background | `--background` | `#f7f8f4` | Page/app background |
| Surface | `--surface` | `#e8eee2` | Cards, panels, modals |
| Text Primary | `--text-primary` | `#194735` | Headings, body text |
| Text Muted | `--text-muted` | `#5e685f` | Captions, placeholders |
| Accent | `--accent` | `#c9dfcf` | CTAs, links, focus rings |
| Border | `--border` | `#29362e` | Dividers, card borders |

### Status Colors

| Status | Hex | Use |
|--------|-----|-----|
| Success | `#245c48` | Confirmations, positive trends |
| Warning | `#846a39` | Caution states, pending items |
| Danger | `#85473e` | Errors, destructive actions |

### Extended Palette

- **color-text-success:** `#396148` — Confirmations, positive trend indicators
- **color-text-label:** `#465349`
- **color-border-hover:** `#aebfac`
- **color-danger-hover:** `#6d3932` — Destructive actions, error states
- **color-danger-border:** `#e4d6cd` — Destructive actions, error states
- **color-success-border:** `#b4cdbb` — Confirmations, positive trend indicators
- **color-focus:** `#397558`
- **color-status-closed-text:** `#826456`

### CSS Variable Tokens

```css
--social-border: var(--color-border);
--color-primary: #245c48;
--color-primary-hover: #194735;
--color-primary-active: #143d2d;
--color-surface-secondary: #f4f6f1;
--color-surface-muted: #f5f7f1;
--color-text-primary: #29362e;
--color-text-secondary: #67736a;
--color-border: #e0e5dd;
--color-border-subtle: #e9ede5;
--color-border-strong: #d8dfd4;
--color-border-accent: #c9dfcf;
--color-border-selected: #eaf0e6;
--color-border-muted: #dbe2d6;
--color-border-hover: #aebfac;
--color-badge-border: #e1e8db;
--color-danger-border: #e4d6cd;
--color-danger-card-border: #e5dbd6;
--color-success-border: #b4cdbb;
--color-ai-border: #c7d6e8;
```

## Typography

### Font Stack

- **inherit** — Heading 1, Heading 2, Heading 3, Body, Caption

### Type Scale

| Role | Family | Size | Weight |
|------|--------|------|--------|
| Heading 1 | inherit | 48px / 3rem | 700 |
| Heading 2 | inherit | 32px / 2rem | 600 |
| Heading 3 | inherit | 24px / 1.5rem | 600 |
| Body | inherit | 16px / 1rem | 400 |
| Caption | inherit | 12px / 0.75rem | 400 |

### Typography Rules

- All text uses **inherit** — never add another font family
- Max 3-4 font sizes per screen
- Headings: weight 600-700, body: weight 400
- Use color and opacity for text hierarchy, not additional font sizes
- Line height: 1.5 for body, 1.2 for headings

## Spacing & Layout

### Base Grid: 4px

Every dimension (margin, padding, gap, width, height) must be a multiple of **4px**.

### Spacing Scale

`2, 4, 6, 8, 10, 12, 14, 16, 18, 20, 22, 24` px

### Spacing as Meaning

| Spacing | Use |
|---------|-----|
| 4-8px | Tight: related items (icon + label, avatar + name) |
| 12-16px | Medium: between groups within a section |
| 24-32px | Wide: between distinct sections |
| 48px+ | Vast: major page section breaks |

### Border Radius

Scale: `3px, 4px, 5px, 6px`
Default: `5px`

### Container

Max-width: `100%`, centered with auto margins.

### Breakpoints

| Name | Value |
|------|-------|
| breakpoint-1280px | 1280px |

Mobile-first: design for small screens, layer on responsive overrides.

## Component Patterns

### Card

```css
.card {
  background: #e8eee2;
  border: 1px solid #29362e;
  border-radius: 5px;
  padding: 16px;
  box-shadow: var(--shadow-md);
}
```

```html
<div class="card">
  <h3>Card Title</h3>
  <p>Card content goes here.</p>
</div>
```

### Button

```css
/* Primary */
.btn-primary {
  background: #c9dfcf;
  color: #194735;
  border-radius: 5px;
  padding: 8px 16px;
  font-weight: 500;
  transition: opacity 150ms ease;
}
.btn-primary:hover { opacity: 0.9; }

/* Ghost */
.btn-ghost {
  background: transparent;
  border: 1px solid #29362e;
  color: #194735;
  border-radius: 5px;
  padding: 8px 16px;
}
```

```html
<button class="btn-primary">Get Started</button>
<button class="btn-ghost">Learn More</button>
```

### Input

```css
.input {
  background: #f7f8f4;
  border: 1px solid #29362e;
  border-radius: 5px;
  padding: 8px 12px;
  color: #194735;
  font-size: 14px;
}
.input:focus { border-color: #c9dfcf; outline: none; }
```

```html
<input class="input" type="text" placeholder="Search..." />
```

### Badge / Chip

```css
.badge {
  display: inline-flex;
  align-items: center;
  padding: 4px 8px;
  border-radius: 9999px;
  font-size: 12px;
  font-weight: 500;
  background: #e8eee2;
  color: #5e685f;
}
```

```html
<span class="badge">New</span>
<span class="badge">Beta</span>
```

### Modal / Dialog

```css
.modal-backdrop { background: rgba(0, 0, 0, 0.6); }
.modal {
  background: #e8eee2;
  border: 1px solid #29362e;
  border-radius: 6px;
  padding: 24px;
  max-width: 480px;
  width: 90vw;
  box-shadow: var(--shadow-sm);
}
```

```html
<div class="modal-backdrop">
  <div class="modal">
    <h2>Dialog Title</h2>
    <p>Dialog content.</p>
    <button class="btn-primary">Confirm</button>
    <button class="btn-ghost">Cancel</button>
  </div>
</div>
```

### Table

```css
.table { width: 100%; border-collapse: collapse; }
.table th {
  text-align: left;
  padding: 8px 12px;
  font-weight: 500;
  font-size: 12px;
  color: #5e685f;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  border-bottom: 1px solid #29362e;
}
.table td {
  padding: 12px;
  border-bottom: 1px solid #29362e;
}
```

```html
<table class="table">
  <thead><tr><th>Name</th><th>Status</th><th>Date</th></tr></thead>
  <tbody>
    <tr><td>Item One</td><td>Active</td><td>Jan 1</td></tr>
    <tr><td>Item Two</td><td>Pending</td><td>Jan 2</td></tr>
  </tbody>
</table>
```

### Navigation

```css
.nav {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 12px 16px;
  border-bottom: 1px solid #29362e;
}
.nav-link {
  color: #5e685f;
  padding: 8px 12px;
  border-radius: 5px;
  transition: color 150ms;
}
.nav-link:hover { color: #194735; }
.nav-link.active { color: #c9dfcf; }
```

```html
<nav class="nav">
  <a href="/" class="nav-link active">Home</a>
  <a href="/about" class="nav-link">About</a>
  <a href="/pricing" class="nav-link">Pricing</a>
  <button class="btn-primary" style="margin-left: auto">Get Started</button>
</nav>
```

## Animation & Motion

This project uses **subtle motion**. Transitions smooth state changes without calling attention.

### Motion Guidelines

- **Duration:** 150-300ms for micro-interactions, 300-500ms for page transitions
- **Easing:** `ease-out` for enters, `ease-in` for exits
- **Direction:** Elements enter from bottom/right, exit to top/left
- **Reduced motion:** Always respect `prefers-reduced-motion` — disable animations when set

## Depth & Elevation

### Shadow Tokens

- Raised (cards, buttons): `var(--shadow-md)`
- Raised (cards, buttons): `var(--shadow-sm)`

### Z-Index Scale

`10`

Use these exact values — never invent z-index values.

## Anti-Patterns (Never Do)

- **No gradients** — solid colors only, everywhere
- **No blur effects** — no backdrop-blur, no filter: blur()
- **No zebra striping** — tables and lists use borders for separation
- **No invented colors** — every hex value must come from the palette above
- **No arbitrary spacing** — every dimension is a multiple of 4px
- **No extra fonts** — only inherit are allowed
- **No arbitrary border-radius** — use the scale: 3px, 4px, 5px, 6px
- **No opacity for disabled states** — use muted colors instead

## Workflow

1. **Read** `references/DESIGN.md` before writing any UI code
2. **Pick colors** from the Color System section — never invent new ones
3. **Set typography** — inherit only, using the type scale
4. **Build layout** on the 4px grid — check every margin, padding, gap
5. **Match components** to patterns above before creating new ones
6. **Apply elevation** — use shadow tokens
7. **Validate** — every value traces back to a design token. No magic numbers.

## Brand Spec

- **Brand color:** `#c9dfcf`
- **Brand typeface:** inherit

## Quick Reference

```
Background:     #f7f8f4
Surface:        #e8eee2
Text:           #194735 / #5e685f
Accent:         #c9dfcf
Border:         #29362e
Font:           inherit
Spacing:        4px grid
Radius:         5px
Frameworks:     Tailwind CSS, React
Icons:          Phosphor
Components:     0 detected
```

## When to Trigger

Activate this skill when:
- Creating new components, pages, or visual elements for careergap-frontend
- Writing CSS, Tailwind classes, styled-components, or inline styles
- Building page layouts, templates, or responsive designs
- Reviewing UI code for design consistency
- The user mentions "careergap-frontend" design, style, UI, or theme
- Generating mockups, wireframes, or visual prototypes

---

# Full Reference Files

> Every output file is embedded below. Claude has full design system context from /skills alone.

## Design System Tokens (DESIGN.md)

# careergap-frontend DESIGN.md

> Auto-generated design system — reverse-engineered via static analysis by skillui.
> Frameworks: Tailwind CSS 4.3.3 + React 19.3.1
> Colors: 20 · Fonts: 1 · Components: 0
> Icon library: Phosphor · State: not detected
> Primary theme: light · Dark mode toggle: no · Motion: subtle

---

## 1. Visual Theme & Atmosphere

This is a **light-themed** interface with a neutral, approachable feel. The light background emphasizes content clarity. Typography uses **inherit** throughout — a clean, modern choice that maintains consistency. Spacing follows a **4px base grid** (compact density), with scale: 2, 4, 6, 8, 10, 12, 14, 16px. The accent color **#c9dfcf** anchors interactive elements (buttons, links, focus rings). Motion is subtle — smooth transitions (150-300ms) ease state changes without drawing attention.

---

## 2. Color Palette & Roles

| Token | Hex | Role | Use |
|---|---|---|---|
| color-bg | `#f7f8f4` | background | Page background, darkest surface |
| color-surface-active | `#e8eee2` | surface | Card and panel backgrounds |
| color-border-strong | `#d8dfd4` | surface | Card and panel backgrounds |
| color-surface | `#ffffff` | surface | Card and panel backgrounds |
| color-primary-hover | `#194735` | text-primary | Headings and body text |
| color-text-tertiary | `#5e685f` | text-muted | Captions, placeholders, secondary info |
| color-text-secondary | `#67736a` | text-muted | Captions, placeholders, secondary info |
| color-text-primary | `#29362e` | border | Dividers, card borders, outlines |
| color-border-accent | `#c9dfcf` | accent | CTAs, links, focus rings, active states |
| color-danger | `#85473e` | danger | Error states, destructive actions |
| color-primary | `#245c48` | success | Success states, positive indicators |
| color-status-draft-text | `#846a39` | warning | Warning states, caution indicators |
| color-text-success | `#396148` | unknown | Palette color |
| color-text-label | `#465349` | unknown | Palette color |
| color-border-hover | `#aebfac` | unknown | Palette color |
| color-danger-hover | `#6d3932` | unknown | Palette color |
| color-danger-border | `#e4d6cd` | unknown | Palette color |
| color-success-border | `#b4cdbb` | unknown | Palette color |
| color-focus | `#397558` | unknown | Palette color |
| color-status-closed-text | `#826456` | unknown | Palette color |

### CSS Variable Tokens

```css
--social-border: var(--color-border);
--color-primary: #245c48;
--color-primary-hover: #194735;
--color-primary-active: #143d2d;
--color-surface-secondary: #f4f6f1;
--color-surface-muted: #f5f7f1;
--color-text-primary: #29362e;
--color-text-secondary: #67736a;
--color-border: #e0e5dd;
--color-border-subtle: #e9ede5;
--color-border-strong: #d8dfd4;
--color-border-accent: #c9dfcf;
--color-border-selected: #eaf0e6;
--color-border-muted: #dbe2d6;
--color-border-hover: #aebfac;
--color-badge-border: #e1e8db;
--color-danger-border: #e4d6cd;
--color-danger-card-border: #e5dbd6;
--color-success-border: #b4cdbb;
--color-ai-border: #c7d6e8;
```


---

## 3. Typography Rules

**Font Stack:**
- **inherit** — Heading 1, Heading 2, Heading 3, Body, Caption

| Role | Font | Size | Weight |
|---|---|---|---|
| Heading 1 | inherit | 48px / 3rem | 700 |
| Heading 2 | inherit | 32px / 2rem | 600 |
| Heading 3 | inherit | 24px / 1.5rem | 600 |
| Body | inherit | 16px / 1rem | 400 |
| Caption | inherit | 12px / 0.75rem | 400 |

**Typographic Rules:**
- Use **inherit** for all text — do not mix font families
- Maintain consistent hierarchy: no more than 3-4 font sizes per screen
- Headings use bold (600-700), body uses regular (400)
- Line height: 1.5 for body text, 1.2 for headings
- Use color and opacity for secondary hierarchy, not additional font sizes


---

## 4. Component Stylings

No components detected. Scan `src/components/` or `components/` to populate this section.

---

## 5. Layout Principles

- **Base spacing unit:** 4px
- **Spacing scale:** 2, 4, 6, 8, 10, 12, 14, 16, 18, 20, 22, 24
- **Border radius:** 3px, 4px, 5px, 6px
- **Max content width:** 100%
- **Container:** Tailwind `container` class with responsive padding

**Spacing as Meaning:**
| Spacing | Use |
|---|---|
| 4-8px | Tight: related items within a group |
| 12-16px | Medium: between groups |
| 24-32px | Wide: between sections |
| 48px+ | Vast: major section breaks |


---

## 6. Depth & Elevation

### Raised — cards, buttons, interactive elements

- `var(--shadow-md)`
- `var(--shadow-sm)`

### Z-Index Scale

`10`



---

## 7. Animation & Motion

This project uses **subtle motion**. Transitions smooth state changes without demanding attention.

### Motion Guidelines

- Duration: 150-300ms for micro-interactions, 300-500ms for page transitions
- Easing: `ease-out` for enters, `ease-in` for exits
- Always respect `prefers-reduced-motion`


---

## 8. Do's and Don'ts

### Do's

- Use `#c9dfcf` for interactive elements (buttons, links, focus rings)
- Use `#f7f8f4` as the primary page background
- Use **inherit** for all UI text
- Follow the **4px** spacing grid for all margins, padding, and gaps
- Use the defined shadow tokens for elevation — see Section 6
- Use border-radius from the scale: 3px, 4px, 5px, 6px
- Use **Phosphor** for all icons

### Don'ts

- Don't introduce colors outside this palette — extend the design tokens first
- Don't mix font families — use inherit consistently
- Don't use arbitrary spacing values — stick to multiples of 4px
- Don't create custom box-shadow values outside the system tokens
- Don't use gradients — the design uses solid colors only
- Don't use arbitrary border-radius values — pick from the defined scale
- Don't mix icon libraries — consistency matters
- Don't use backdrop-blur or blur effects

### Anti-Patterns (detected from codebase)

- No gradient backgrounds
- No blur or backdrop-blur effects
- No zebra striping on tables/lists


---

## 9. Responsive Behavior

| Name | Value | Source |
|---|---|---|
| breakpoint-1280px | 1280px | css |

**Approach:** Mobile-first using Tailwind responsive prefixes (`sm:`, `md:`, `lg:`, `xl:`, `2xl:`).
Always design for mobile first, then layer on responsive overrides.


---

## 10. Agent Prompt Guide

Use these as starting points when building new UI:

### Build a Card

```
Background: #e8eee2
Border: 1px solid #29362e
Radius: 5px
Padding: 16px
Font: inherit
Use shadow tokens from Section 6.
```

### Build a Button

```
Primary: bg #c9dfcf, text white
Ghost: bg transparent, border #29362e
Padding: 8px 16px
Radius: 5px
Hover: opacity 0.9 or lighter shade
Focus: ring with #c9dfcf
```

### Build a Page Layout

```
Background: #f7f8f4
Max-width: 100%, centered
Grid: 4px base
Responsive: mobile-first, breakpoints from Section 9
```

### Build a Stats Card

```
Surface: #e8eee2
Label: #5e685f (muted, 12px, uppercase)
Value: #194735 (primary, 24-32px, bold)
Status: use success/warning/danger from Section 2
```

### Build a Form

```
Input bg: #f7f8f4
Input border: 1px solid #29362e
Focus: border-color #c9dfcf
Label: #5e685f 12px
Spacing: 16px between fields
Radius: 5px
```

### General Component

```
1. Read DESIGN.md Sections 2-6 for tokens
2. Colors: only from palette
3. Font: inherit, type scale from Section 3
4. Spacing: 4px grid
5. Components: match patterns from Section 4
6. Elevation: shadow tokens
```

