# RivianMate Design System

## Design Philosophy

**Core Concept: "Adventure Data Dashboard"**

RivianMate should feel like a companion for the adventurous Rivian owner - someone who cares about their vehicle, loves data, but doesn't want to be overwhelmed. The design should evoke:

- **Outdoor adventure** - topographic patterns, terrain, horizon lines
- **Clean technology** - high contrast, legible data, no clutter  
- **Warmth & approachability** - not cold/clinical, inviting to use daily
- **Premium quality** - matching Rivian's brand positioning

---

## Typography

### Primary Typeface: **Söhne** (or alternatives)

Rivian's brand uses a custom typeface adapted from **Söhne** by Klim Type Foundry. Since we can't use their proprietary font, here are excellent alternatives that capture the same spirit:

**Recommended Stack (in order of preference):**

1. **Outfit** (Google Fonts) - Geometric, clean, very similar feel to Söhne
2. **Plus Jakarta Sans** (Google Fonts) - Slightly warmer, excellent for UI
3. **Manrope** (Google Fonts) - Modern geometric sans with subtle personality
4. **DM Sans** (Google Fonts) - Clean and neutral, good for data

**Monospace for Numbers/Data:**
- **JetBrains Mono** or **IBM Plex Mono** - For battery percentages, kWh values, odometer readings

### Type Scale

```css
--text-xs: 0.75rem;    /* 12px - Labels, captions */
--text-sm: 0.875rem;   /* 14px - Secondary text */
--text-base: 1rem;     /* 16px - Body text */
--text-lg: 1.125rem;   /* 18px - Emphasized body */
--text-xl: 1.25rem;    /* 20px - Section headers */
--text-2xl: 1.5rem;    /* 24px - Card titles */
--text-3xl: 1.875rem;  /* 30px - Page headers */
--text-4xl: 2.25rem;   /* 36px - Hero numbers */
--text-5xl: 3rem;      /* 48px - Large data displays */
```

### Usage Guidelines

- **Large numbers** (battery %, range, health): Display font at `text-4xl` or `text-5xl`, tabular numerals
- **Labels**: `text-sm`, slightly muted color, uppercase tracking for categories
- **Body text**: `text-base`, comfortable line height (1.5-1.6)
- **Never underline links** - use color or subtle background instead

---

## Color Palette

### Inspired by Rivian's Adventure Aesthetic

**Primary Colors:**

```css
/* Compass Yellow - Rivian's signature accent */
--rivian-yellow: #DEB526;
--rivian-yellow-light: #F5D45A;
--rivian-yellow-dark: #B8940F;

/* Forest Greens - Adventure/Nature */
--forest-deep: #1A2F1A;
--forest-mid: #2D4A2D;
--forest-light: #4A7C4A;
--forest-muted: #6B8E6B;

/* Earth Tones - Grounding */
--earth-brown: #8B7355;
--earth-sand: #C4A77D;
--earth-clay: #A67B5B;

/* Sky/Water - Horizon, freedom */
--sky-blue: #5B8FA8;
--water-deep: #2C5F7C;
--water-light: #89B4C8;
```

**Neutral Palette:**

```css
/* Dark Mode Base (Recommended Primary) */
--neutral-950: #0A0A0A;  /* Deepest background */
--neutral-900: #121212;  /* Card backgrounds */
--neutral-850: #1A1A1A;  /* Elevated surfaces */
--neutral-800: #262626;  /* Borders, dividers */
--neutral-700: #404040;  /* Subtle borders */
--neutral-600: #525252;  /* Disabled state */
--neutral-500: #737373;  /* AVOID for text - too low contrast */
--neutral-400: #A3A3A3;  /* Secondary text, labels (minimum for readability) */
--neutral-300: #D4D4D4;  /* Primary body text */
--neutral-200: #E5E5E5;  /* Emphasized text */
--neutral-100: #F5F5F5;  /* Headings, important data */
--neutral-50: #FAFAFA;   /* Bright accents */

/* CONTRAST RULES:
   - Primary text: Use neutral-100 or neutral-200 (high contrast)
   - Secondary/labels: Use neutral-400 minimum (not 500!)
   - Units after numbers: Use neutral-400
   - Disabled/inactive: Use neutral-500 only here
   - Never use neutral-500 or darker for readable text
*/

/* Light Mode Alternative */
--light-bg: #FAFAFA;
--light-surface: #FFFFFF;
--light-border: #E5E5E5;
--light-text: #1A1A1A;
--light-muted: #525252;  /* Use 600 not 500 for light mode muted */
```

**Semantic Colors:**

```css
/* Battery Health Status */
--health-excellent: #22C55E;  /* 95%+ - Vibrant green */
--health-good: #84CC16;       /* 90-95% - Lime */
--health-fair: #EAB308;       /* 80-90% - Yellow/amber */
--health-warning: #F97316;    /* 70-80% - Orange */
--health-poor: #EF4444;       /* <70% - Red */

/* Charging States */
--charging-active: #22C55E;
--charging-scheduled: #3B82F6;
--charging-complete: #8B5CF6;
--charging-error: #EF4444;

/* General Feedback */
--success: #22C55E;
--warning: #F59E0B;
--error: #EF4444;
--info: #3B82F6;
```

### Color Usage Guidelines

1. **Dark mode first** - Most EV owners check their apps at night
2. **Yellow sparingly** - Use Rivian yellow for key CTAs and important data only
3. **Green = positive** - Health, efficiency, charging
4. **High contrast data** - Numbers should POP against backgrounds
5. **Subtle gradients** - Use very subtle gradients for depth, never garish

---

## Visual Motifs

### Adventure-Inspired Patterns

**Topographic Lines:**
- Subtle background pattern using layered curves
- Represents terrain, adventure, exploration
- Use at 5-10% opacity as background texture

```css
/* Example: subtle topo pattern */
.topo-background {
  background-image: url("data:image/svg+xml,...");
  background-size: 200px;
  opacity: 0.05;
}
```

**Horizon Gradients:**
- Subtle gradients from darker (bottom) to lighter (top)
- Evokes sunrise/sunset, open landscapes

**Compass Rose:**
- Use sparingly as a decorative element
- References Rivian's logo symbolism
- Good for loading states or empty states

**Geometric Mountains:**
- Simple triangular shapes suggesting peaks
- Can frame data sections or act as dividers

---

## Component Design

### Cards

```
┌─────────────────────────────────────┐
│                                     │
│  ⚡ Battery Health                  │  ← Category label (muted, uppercase)
│                                     │
│      97.2%                          │  ← Hero number (large, bold)
│                                     │
│  ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━  │  ← Progress bar (health-colored)
│                                     │
│  128.4 kWh  ·  Est. 131 kWh new     │  ← Supporting data (smaller, muted)
│                                     │
└─────────────────────────────────────┘
```

**Card Guidelines:**
- Generous padding (24-32px)
- Subtle border or shadow, never both
- One primary metric per card
- Rounded corners (12-16px)
- Background slightly elevated from page

### Data Displays

**Large Numbers:**
```css
.hero-number {
  font-size: 3rem;
  font-weight: 700;
  font-variant-numeric: tabular-nums;
  letter-spacing: -0.02em;
  line-height: 1;
}

.unit {
  font-size: 1rem;
  font-weight: 400;
  color: var(--neutral-500);
  margin-left: 0.25rem;
}
```

**Progress/Health Bars:**
- Height: 8-12px
- Rounded ends
- Smooth gradient fill based on value
- Subtle background track

### Navigation

**Sidebar (Desktop):**
- Fixed left sidebar, 240-280px wide
- Logo at top
- Navigation items with icons
- Active state: subtle background + accent border-left
- Collapse to icons only on medium screens

**Bottom Nav (Mobile):**
- Fixed bottom bar
- 4-5 primary destinations
- Active state: icon color change + label

### Charts & Graphs

**Line Charts (Battery Health Over Time):**
- Clean grid lines at 5-10% opacity
- Smooth curves, not angular
- Fill gradient under line (subtle)
- Interactive tooltips on hover
- Accent color for primary line

**Gauge/Ring Charts (Current Health):**
- Thick stroke (12-16px)
- Health-colored based on value
- Center: large number + label
- Subtle tick marks at key points (70%, 90%, 100%)

---

## Layout Principles

### Grid System

```css
/* Desktop: 12-column grid */
.container {
  max-width: 1440px;
  margin: 0 auto;
  padding: 0 2rem;
}

/* Dashboard Grid */
.dashboard-grid {
  display: grid;
  grid-template-columns: repeat(12, 1fr);
  gap: 1.5rem;
}

/* Common layouts */
.card-full { grid-column: span 12; }
.card-half { grid-column: span 6; }
.card-third { grid-column: span 4; }
.card-quarter { grid-column: span 3; }
```

### Spacing Scale

```css
--space-1: 0.25rem;   /* 4px */
--space-2: 0.5rem;    /* 8px */
--space-3: 0.75rem;   /* 12px */
--space-4: 1rem;      /* 16px */
--space-5: 1.25rem;   /* 20px */
--space-6: 1.5rem;    /* 24px */
--space-8: 2rem;      /* 32px */
--space-10: 2.5rem;   /* 40px */
--space-12: 3rem;     /* 48px */
--space-16: 4rem;     /* 64px */
```

### Responsive Breakpoints

```css
--breakpoint-sm: 640px;   /* Mobile landscape */
--breakpoint-md: 768px;   /* Tablet */
--breakpoint-lg: 1024px;  /* Desktop */
--breakpoint-xl: 1280px;  /* Large desktop */
--breakpoint-2xl: 1536px; /* Extra large */
```

---

## Interaction Design

### Animations

**Page Transitions:**
- Fade in: 200-300ms ease-out
- Stagger children: 50ms delay each

**Card Hover:**
- Subtle lift (translateY -2px)
- Soft shadow increase
- 150ms transition

**Data Updates:**
- Number changes: count up/down animation
- Progress bars: smooth width transition
- Charts: draw-in animation on load

**Loading States:**
- Skeleton screens matching content shape
- Subtle shimmer animation
- Compass rose spinner for longer loads

### Micro-interactions

- Button press: scale to 0.98
- Toggle switches: smooth slide with spring
- Tooltips: fade in with slight upward motion
- Success feedback: subtle green flash

---

## Page Layouts

### Dashboard (Main View)

```
┌──────────────────────────────────────────────────────────────────┐
│  ┌──────────┐                                                    │
│  │  LOGO    │  RivianMate              [Settings] [User Avatar]  │
│  └──────────┘                                                    │
├──────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ┌─────────────────────────────────────────────────────────────┐ │
│  │                                                             │ │
│  │  🚗 R1T · Compass Yellow                    Last seen: Now  │ │
│  │                                                             │ │
│  │  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐         │ │
│  │  │   73%       │  │   234 mi    │  │   97.2%     │         │ │
│  │  │  Charge     │  │   Range     │  │   Health    │         │ │
│  │  └─────────────┘  └─────────────┘  └─────────────┘         │ │
│  │                                                             │ │
│  └─────────────────────────────────────────────────────────────┘ │
│                                                                  │
│  ┌──────────────────────────────┐  ┌──────────────────────────┐ │
│  │  Battery Health Over Time    │  │  Recent Charging         │ │
│  │                              │  │                          │ │
│  │  [Line Chart]                │  │  • Home · 45 kWh · 2hr   │ │
│  │                              │  │  • DCFC · 62 kWh · 28min │ │
│  │                              │  │  • Home · 38 kWh · 1.5hr │ │
│  └──────────────────────────────┘  └──────────────────────────┘ │
│                                                                  │
│  ┌──────────────────────────────┐  ┌──────────────────────────┐ │
│  │  Efficiency This Month       │  │  Quick Stats             │ │
│  │                              │  │                          │ │
│  │  2.4 mi/kWh avg              │  │  Total Miles: 12,456     │ │
│  │                              │  │  This Month: 842 mi      │ │
│  └──────────────────────────────┘  └──────────────────────────┘ │
│                                                                  │
└──────────────────────────────────────────────────────────────────┘
```

### Battery Health Detail Page

```
┌──────────────────────────────────────────────────────────────────┐
│                                                                  │
│  ← Back to Dashboard                                             │
│                                                                  │
│  Battery Health                                                  │
│  ─────────────────────────────────────────────────────────────── │
│                                                                  │
│  ┌────────────────────────────────────────────────────────────┐ │
│  │                                                            │ │
│  │           ╭───────────────────╮                            │ │
│  │          ╱                     ╲                           │ │
│  │         │       97.2%          │         Excellent         │ │
│  │         │     128.4 kWh        │         Condition         │ │
│  │          ╲                     ╱                           │ │
│  │           ╰───────────────────╯                            │ │
│  │                                                            │ │
│  │   Original: 131 kWh  ·  Lost: 2.6 kWh  ·  12,456 miles    │ │
│  │                                                            │ │
│  └────────────────────────────────────────────────────────────┘ │
│                                                                  │
│  ┌────────────────────────────────────────────────────────────┐ │
│  │  Capacity Over Time                                        │ │
│  │                                                            │ │
│  │  131 ─┬─────────────────────────────────────────────────   │ │
│  │       │ ●                                                  │ │
│  │  128 ─┼───●────●─────●────●────●────●────●────●           │ │
│  │       │                                                    │ │
│  │  125 ─┼─────────────────────────────────────────────────   │ │
│  │       └────┬────┬────┬────┬────┬────┬────┬────┬────────   │ │
│  │          Jan   Mar  May  Jul  Sep  Nov  Jan  Mar           │ │
│  │                                                            │ │
│  └────────────────────────────────────────────────────────────┘ │
│                                                                  │
│  ┌─────────────────────────┐  ┌────────────────────────────────┐│
│  │  Projections            │  │  Warranty Status               ││
│  │                         │  │                                ││
│  │  At 100k mi: 94.2%      │  │  ✓ 70% threshold: ~450k mi    ││
│  │  At 150k mi: 91.8%      │  │  ✓ Well within warranty       ││
│  │  Rate: 0.8% / 10k mi    │  │                                ││
│  └─────────────────────────┘  └────────────────────────────────┘│
│                                                                  │
└──────────────────────────────────────────────────────────────────┘
```

---

## Dark Mode vs Light Mode

**Primary: Dark Mode**
- Better for nighttime checking (common use case)
- Matches Rivian's in-vehicle UI
- Easier on battery for OLED screens
- More dramatic data visualization

**Secondary: Light Mode**
- Toggle available in settings
- Clean whites and subtle grays
- Same accent colors, adjusted for contrast
- Good for daytime/outdoor use

---

## Implementation Notes

### CSS Framework

Recommend **Tailwind CSS** for rapid development with design tokens, or hand-crafted CSS with CSS custom properties for full control.

### Charting Library

- **Recharts** (React) - Clean, customizable
- **Chart.js** - Lightweight, good animations
- **Plotly** - Advanced, interactive (overkill for MVP)

### Icons

- **Lucide** - Clean, consistent, Rivian-like simplicity
- Or custom SVG icons for vehicle-specific elements

### Accessibility

- Minimum contrast ratio: 4.5:1 for text
- Focus states on all interactive elements
- Reduced motion option
- Semantic HTML structure
- ARIA labels for data visualizations

---

## Design Tokens (CSS Variables)

```css
:root {
  /* Colors */
  --color-primary: #DEB526;
  --color-primary-hover: #F5D45A;
  --color-background: #0A0A0A;
  --color-surface: #121212;
  --color-surface-elevated: #1A1A1A;
  --color-border: #262626;
  --color-text-primary: #F5F5F5;      /* For headings, important numbers */
  --color-text-secondary: #D4D4D4;    /* For body text */
  --color-text-muted: #A3A3A3;        /* For labels, hints - NOT darker! */
  --color-text-disabled: #737373;     /* Only for truly disabled elements */
  
  /* Health colors */
  --color-health-excellent: #22C55E;
  --color-health-good: #84CC16;
  --color-health-fair: #EAB308;
  --color-health-warning: #F97316;
  --color-health-poor: #EF4444;
  
  /* Typography */
  --font-family-sans: 'Outfit', 'Plus Jakarta Sans', system-ui, sans-serif;
  --font-family-mono: 'JetBrains Mono', 'IBM Plex Mono', monospace;
  
  /* Spacing */
  --radius-sm: 0.375rem;
  --radius-md: 0.75rem;
  --radius-lg: 1rem;
  --radius-xl: 1.5rem;
  
  /* Shadows */
  --shadow-sm: 0 1px 2px rgba(0, 0, 0, 0.5);
  --shadow-md: 0 4px 6px rgba(0, 0, 0, 0.4);
  --shadow-lg: 0 10px 15px rgba(0, 0, 0, 0.3);
  
  /* Transitions */
  --transition-fast: 150ms ease;
  --transition-base: 200ms ease;
  --transition-slow: 300ms ease;
}

/* Contrast Reference (WCAG AA minimum 4.5:1 for text):
   - neutral-400 (#A3A3A3) on neutral-900 (#121212) = 6.4:1 ✓
   - neutral-500 (#737373) on neutral-900 (#121212) = 4.0:1 ✗ FAIL
   - neutral-100 (#F5F5F5) on neutral-900 (#121212) = 14.5:1 ✓
   
   Rule: Always use neutral-400 or lighter for any readable text!
*/
```

---

## Next Steps

1. **Create Figma/design mockups** for key screens
2. **Build component library** in Blazor with these styles
3. **Implement dark mode first**, then light mode toggle
4. **Add charts** with health visualization
5. **Test on mobile** - responsive from day one
