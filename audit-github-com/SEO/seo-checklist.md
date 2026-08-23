# SEO Checklist — GitHub Repo + Future Landing Site

## On-GitHub (do first, ~1 hour)

- [ ] **Add topics** (highest-leverage GitHub SEO action):
      `azerothcore world-of-warcraft wow wotlk wrath-of-the-lich-king 335a
      playerbots private-server installer one-click winforms dotnet csharp
      windows gaming self-hosted`
- [ ] **Shorten description** to ~120 chars with the phrase
      "AzerothCore 3.3.5a" verbatim (users search the canonical spelling).
- [ ] **Set social preview** image (1280×640, <1 MB) under
      Settings → General → Social preview.
- [ ] **Add Website/homepage** link (Releases page, then GitHub Pages site).
- [ ] **Enable Discussions** (boosts engagement signals + support deflection).
- [ ] **First H1 in README = target keyword phrase**:
      "Azaroth Core — One-Click Installer for AzerothCore 3.3.5a + PlayerBots"
      (it already is — keep it).
- [ ] **Add badges** (build, license, release, .NET) — they render on third-party
      mirrors and improve CTR.
- [ ] **Add screenshot(s)** in README — images appear in some search/social
      previews and increase time-on-page.
- [ ] Use **absolute URLs** for cross-doc links so the README renders correctly
      when mirrored.
- [ ] **Tag a release** with full release notes + checksums; GitHub indexes
      `/releases` and release notes.

## Naming / keyword decision

The product is "**Azaroth** Core" but installs "**Azeroth**Core". Users search the
canonical spelling. Options:
1. Rename to **"AzerothCore One-Click Installer"** (best for SEO; sacrifices brand).
2. Keep "Azaroth Core" but ensure the H1, description, topics, and first paragraph
   all contain "AzerothCore 3.3.5a" (current approach — acceptable).

## GitHub Pages site (medium effort, high value)

Use `HTML/index.html`, `CSS/styles.css`, `Metadata/meta-tags.html`,
`Schema/software-application.jsonld`, `Schema/faq.jsonld`, `Robots/robots.txt`,
`Robots/sitemap.xml` from this audit.

- [ ] One `<title>` per page, ≤60 chars, leading keyword.
- [ ] Unique meta description per page, 140–160 chars, with a value prop + CTA.
- [ ] One H1; logical H2/H3 hierarchy (already in the template).
- [ ] Canonical tags; `lang="en"`; Open Graph + Twitter cards.
- [ ] `SoftwareApplication` + `FAQPage` JSON-LD.
- [ ] `robots.txt` + `sitemap.xml`; submit sitemap to Google Search Console /
      Bing Webmaster.
- [ ] Optimized banner (WebP/AVIF + fallback), lazy-load below-the-fold images.
- [ ] Mobile-first responsive layout (the CSS template is).
- [ ] Add a docs/ section with the CONFIG and TROUBLESHOOTING content; each doc
      targets a long-tail query.

## Content / keyword opportunities

Target queries (informational → transactional):
- "azerothcore one click installer" (transactional — homepage)
- "how to set up a private wow 3.3.5a server" (informational — guide/blog)
- "azerothcore playerbots setup" (informational — doc)
- "wotlk 3.3.5a repack with bots" (commercial/community — comparison)
- "azerothcore windows installer" (transactional)

Produce one long-form guide:
**"How to set up a private WoW 3.3.5a server with PlayerBots in 2026 (no
terminal)"** — step-by-step with screenshots, ending in a download CTA. Host on
GitHub Pages; link from README and relevant Reddit/forum posts (where allowed).

## Off-page (slow burn)

- [ ] Link from the AzerothCore wiki/community where relevant (respect their rules).
- [ ] Answer relevant Reddit / OwnedCore / AC forum threads with useful advice +
      a link (no spam).
- [ ] A Discord (or use Discussions) generates engagement and repeat traffic.
- [ ] Release notes/changelog get natural links over time.

## Measurement

- [ ] Google Search Console for the Pages site (impressions/queries/CTR).
- [ ] GitHub Insights → Traffic (referrers, clones, unique cloners).
- [ ] Release download counts per version.
