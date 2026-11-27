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
