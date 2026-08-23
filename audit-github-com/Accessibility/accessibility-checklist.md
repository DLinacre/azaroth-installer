# Accessibility Checklist (WCAG 2.2 AA) — Azaroth Core installer + docs

Web/README items pass largely because GitHub provides semantics, keyboard nav and
themes. The WinForms installer is where work is needed.

## Perceivable (1.x)

- [ ] **1.1.1 Non-text content** — Banner has `alt` (✅). In wizard, every
      non-decorative glyph/icon needs an accessible name or text label.
- [ ] **1.3.1 Info & relationships** — Nav "labels" must become real buttons;
      ListView columns need accessible names; step status must not rely on color.
- [ ] **1.3.2 Meaningful sequence** — Tab order follows visual order (welcome →
      controls → actions); verify after z-order changes.
- [ ] **1.4.1 Use of color** — Completed/current/future steps use text + icon, not
      color only; errors use `ErrorProvider` icon + message.
- [ ] **1.4.3 Contrast (Minimum)** — Audit all pairs:
      - log text ≥ 4.5:1 (currently borderline at 8.5 pt);
      - grey Back button `#fff` on `#3C404C` ≈ 4.6:1 (bump to `#4a4f5e`);
      - muted/silver labels on dark surfaces.
- [ ] **1.4.4 Resize text** — App scales at 150%/200% without clipping
      (`PerMonitorV2`, avoid fixed `Size` where possible).
- [ ] **1.4.10 Reflow** — At 125% on a 960-wide window, no content is clipped or
      requires horizontal scrolling.
- [ ] **1.4.11 Non-text contrast** — Buttons/checkboxes/focus indicators have
      ≥ 3:1 against adjacent surfaces.
- [ ] **1.4.12 Text spacing** — User text-size settings honored.

## Operable (2.x)

- [ ] **2.1.1 Keyboard** — Every action reachable by Tab; Enter activates primary,
      Esc cancels; nav items are Buttons.
- [ ] **2.1.2 No keyboard trap** — Log TextBox is read-only but must not trap
      focus (Ctrl+Tab / F6 out).
- [ ] **2.4.3 Focus order** — Logical order; set `TabIndex` explicitly on each
      step rather than relying on add-order.
- [ ] **2.4.6 Headings and labels** — Each step's title describes the step; form
      labels are visible (not only placeholder).
- [ ] **2.4.7 Focus visible** — Custom focus ring on dark-themed buttons; the
      default WinForms dotted rectangle is invisible on custom BackColor.
- [ ] **2.4.11 Focus not obscured** — Sticky/footer buttons don't cover focused
      controls (the fixed footer is 126 px — ensure scroll padding).
- [ ] **2.5.1 Pointer gestures** — No swipes/drag-only actions; clicks and
      keyboard both work.
- [ ] **2.5.3 Label in name** — Buttons announce their visible text ("Full Auto
      Install", not only "⚡").
- [ ] **2.5.7 Dragging movements** — Any drag (none currently) has a click
      alternative.

## Understandable (3.x)

- [ ] **3.2.1 On focus / 3.2.2 On input** — Changing a control doesn't
      unexpectedly advance/reset the wizard.
- [ ] **3.3.1 Error identification** — Field errors shown inline via
      `ErrorProvider`, not only in the log.
- [ ] **3.3.2 Labels or instructions** — Required format (e.g. realm name length,
      password rules) stated before submission.
- [ ] **3.3.3 Error suggestion** — e.g. "Password must be at least 8 characters."
- [ ] **3.3.4 Error prevention** — Destructive actions (re-import DB, uninstall)
      require confirmation with a summary of consequences.

## Robust (4.x)

- [ ] **4.1.2 Name, role, value** — All controls expose `AccessibleName` /
      `AccessibleRole`; custom ListViews and the module picker expose selection
      state; live log updates announced via `AccessibleEvents` if critical.
- [ ] **4.1.3 Status messages** — When a step completes/fails, set
      `AccessibleName`/live status so screen readers announce it (e.g.
      "Database ready" / "Verification failed — see log").

## Test process

1. Keyboard-only walkthrough of all 9 steps.
2. NVDA (Windows) and Windows Narrator pass.
3. Contrast checker on every custom ForeColor/BackColor pair.
4. DPI 100/125/150/200% and 1366×768 / 4K.
5. Windows High Contrast modes (#1/#2 and custom).
