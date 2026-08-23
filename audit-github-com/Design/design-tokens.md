# Design System Notes (for `src/Theme.cs` + landing page)

## Color

| Token | Hex | Use |
|---|---|---|
| `--bg` | `#12141A` | App/page background |
| `--surface` | `#181B24` | Nav, cards (web) |
| `--surface-2` | `#20242F` | Raised cards |
| `--border` | `#2C3142` | Dividers, outlines |
| `--text` | `#E9EBF2` | Primary text (~11:1) |
| `--muted` | `#B3B9C9` | Secondary text (~7.5:1) |
| `--gold` | `#FFC457` | Brand accent, headings |
| `--gold-contrast` | `#1A1407` | Text on gold |
| `--primary` | `#2F9A5A` | Primary CTA (AAA on bg) |
| `--primary-hover` | `#37B268` | CTA hover |
| `--danger` | `#E06161` | Destructive / errors |
| `--info` | `#7CC4FF` | Links / info |
| `--success` | `#5BD18A` | Success (with text+icon, never color alone) |
| `--warning` | `#FFC457` | Warnings (same as gold; add icon) |
| `--focus` | `#FFD166` | Focus ring (3 px, 2 px offset) |

## Typography
- Font: Segoe UI (Windows native) / system stack on web; Consolas only for logs,
  min **9 pt**, with AA contrast.
- Scale: 12 / 14 / 16 (body) / 20 (card h) / 24 (step title) / 32+ (page title).

## Spacing (4 px base)
`4, 8, 12, 16, 24, 32, 48, 64`. Use `Padding`/`Margin` from this scale only.

## Radius
- Cards/inputs: `10–12 px`
- Buttons: `10 px`
- Focus ring: `4 px`

## Components to abstract
- `MkCard(title, body)` — titled surface with consistent padding/border.
- `MkPrimaryButton`, `MkSecondaryButton`, `MkDangerButton`, `MkGhostButton`
  (flat, consistent hover/focus/disabled, with `AccessibleName`).
- `MkLabel(text, muted)` and `MkHelp(text)`.
- `MkStepIcon(state)` — completed/current/upcoming using **icon + text + color**.
- `MkPasswordBox(showToggle)` — masked, with confirm field.
- `MkErrorProvider()` — field-level errors wired to `AccessibleDescription`.

## Status representation
- Completed: ✓ + "Completed" (success color + text)
- Current: ● + "Current" (gold + text)
- Upcoming: ○ + "Upcoming" (muted + text)
- Error: ⚠ + "Error — see message" (danger + text)

Never convey state with color or emoji alone.

## High-DPI
- Set `<ApplicationHighDpiMode>PerMonitorV2</ApplicationHighDpiMode>` in csproj.
- Use `Font` sizes in **points**; prefer `TableLayoutPanel` + `Dock`/`Anchor` over
  fixed `Location`; test at 100/125/150/200%.

## Motion
- Keep transitions ≤ 200 ms; respect `prefers-reduced-motion` (web) and Windows
  "Show animations" setting.
