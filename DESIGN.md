# DESIGN.md — DraGon Desktop UI

## Product Intent
DraGon is a lightweight Windows local web development control panel for PHP, Laravel, Node.js, and `.dra` local domains. The interface is a **compact, minimalist, high-density desktop utility** — fast to scan, zero decorative clutter, and clear operational state at a glance.

## Visual Direction

### Color System
Two neutral themes (Dark and Light) built on true neutral charcoal/zinc tones, paired with **one** brand accent (**Dragon Ember**).

#### Dark Theme (Default)
- `WindowBgBrush`: `#0E0E10` (canvas)
- `SidebarBgBrush`: `#141417` (chrome: sidebar and top bar)
- `CardBgBrush`: `#19191D` (primary surface)
- `CardAltBgBrush`: `#222227` (recessed rows, badges, inline strips)
- `InputBgBrush`: `#111114` (text inputs)
- `BorderSubtleBrush`: `#2A2A30` (1px hairline dividers)
- `TextPrimaryBrush`: `#F4F4F5`
- `TextSecondaryBrush`: `#A1A1AA`
- `TextMutedBrush`: `#71717A`
- `AccentPrimaryBrush`: `#EA580C` (hover `#F97316`, soft tint `#261812`)
- `SecondaryBtnBgBrush`: `#222227` (hover `#2C2C33`)
- `TerminalBgBrush`: `#09090B`, `TerminalTextBrush`: `#D4D4D8`

#### Light Theme
- `WindowBgBrush`: `#F6F6F7`
- `SidebarBgBrush`: `#FFFFFF`
- `CardBgBrush`: `#FFFFFF`
- `CardAltBgBrush`: `#F1F1F4`
- `InputBgBrush`: `#FFFFFF`
- `BorderSubtleBrush`: `#E4E4E7`
- `TextPrimaryBrush`: `#18181B`
- `TextSecondaryBrush`: `#52525B`
- `TextMutedBrush`: `#71717A`
- `AccentPrimaryBrush`: `#EA580C` (hover `#C2410C`, soft tint `#FFF3EC`)
- `SecondaryBtnBgBrush`: `#F4F4F5` (hover `#E4E4E7`)
- `TerminalBgBrush`: `#18181B`, `TerminalTextBrush`: `#E4E4E7`

#### Semantic Status Colors (Status Indicators Only)
- Success / Running / Synced: `#10B981`
- Warning / Missing / Pending: `#D97706`
- Danger / Error / Conflict: `#DC2626`
- Info / Active URL: `#60A5FA` (Dark), `#2563EB` (Light)

### Accent Discipline
- Reserve `AccentPrimaryBrush` for: active sidebar indicator, primary action buttons (`Start`, `+ Proyek`, `Terapkan`, `Simpan`), and focused text input borders.
- Never color-code sidebar navigation icons in multiple colors.

### Typography
- Primary UI: `Segoe UI Variable Text, Segoe UI`
- Monospace (paths, ports, PIDs, logs, PHP extensions): `Cascadia Mono, Consolas`
- Compact Type Scale:
  - View Heading: `14px` SemiBold
  - Card / Group Heading: `12.5px` SemiBold
  - Body / Buttons / Inputs: `11.5px`
  - Metadata / Tags / Monospace: `10.5px`

### Compact Geometry & Rhythm
- Window default: `680 × 400` (`MinWidth="540" MinHeight="340"`).
- Sidebar width: `118px`.
- Border radius: `4px` (buttons, inputs, tags), `6px` (cards, banners).
- Flat elevation: no drop shadows, no glow, no gradients. Separation uses `1px` hairline borders and tonal surface steps.
- Card padding: `10,8` (compact); gap between stacked cards: `6px`–`8px`.

### Anti-Slop Constraints
- **No decorative emojis** in buttons, tabs, labels, or banners. Use plain text labels and minimal standard control symbols (`▶`, `■`, `↻`, `+`, `×`) only where functional.
- **Concise UI copy**: short operational labels, no marketing fluff or redundant multi-sentence explanations.
- **Clean code comments** (`antislop-code`): no decorative `====` banners, no obvious restating comments, and no emoji in comments.
