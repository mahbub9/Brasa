import { defineConfig } from 'vitepress'

// Documentation site for Brasa, published to GitHub Pages.
//
// The source of truth is the markdown in docs/ — the same files a developer
// reads when browsing the repository. This config only decides how they are
// presented; it never holds content of its own.
//
//   npm run docs:dev       local preview with hot reload
//   npm run docs:build     production build into docs/.vitepress/dist
export default defineConfig({
  title: 'Brasa',
  description:
    'Multi-tenant restaurant management SaaS for Portugal, built around an AT-certifiable fiscal engine.',

  // Project site, so everything is served under /Brasa/.
  base: '/Brasa/',

  lang: 'en-GB',
  cleanUrls: true,
  lastUpdated: true,

  // Directory README.md files stay README.md so GitHub renders them when
  // browsing a folder. These rewrites map them to the site's index pages, so
  // both audiences get the right thing from one file.
  rewrites: {
    'README.md': 'index.md',
    ':dir/README.md': ':dir/index.md',
    ':dir/:sub/README.md': ':dir/:sub/index.md',
  },

  // The feature template is scaffolding for authors, not a page.
  srcExclude: ['**/_template.md'],

  head: [
    ['meta', { name: 'theme-color', content: '#c1440e' }],
    ['meta', { name: 'og:type', content: 'website' }],
  ],

  themeConfig: {
    siteTitle: 'Brasa',

    nav: [
      { text: 'Architecture', link: '/architecture/' },
      { text: 'Fiscal', link: '/fiscal/' },
      { text: 'Development', link: '/development/getting-started' },
      { text: 'Features', link: '/features/' },
      {
        text: 'Product',
        items: [
          { text: 'Roadmap', link: '/product/roadmap' },
          { text: 'Backlog & progress', link: '/product/backlog' },
          { text: 'Differentiation', link: '/product/differentiation' },
          { text: 'Build status', link: '/product/status' },
          { text: 'Plan & roadmap', link: '/product/plan' },
          { text: 'Glossary', link: '/glossary' },
        ],
      },
    ],

    sidebar: [
      {
        text: 'Start here',
        items: [
          { text: 'Documentation index', link: '/' },
          { text: 'Getting started', link: '/development/getting-started' },
          { text: 'Glossary', link: '/glossary' },
        ],
      },
      {
        text: 'Session pickup',
        collapsed: false,
        items: [
          { text: 'AI session brief', link: '/ai/' },
          { text: 'Repository map', link: '/ai/repo-map' },
        ],
      },
      {
        text: 'Architecture',
        collapsed: false,
        items: [
          { text: 'Overview', link: '/architecture/' },
          { text: 'API contract', link: '/architecture/api-contract' },
          { text: 'Error code registry', link: '/architecture/error-codes' },
          { text: 'Money', link: '/architecture/money' },
          { text: 'Multi-tenancy', link: '/architecture/multi-tenancy' },
          { text: 'Module boundaries', link: '/architecture/module-boundaries' },
          { text: 'Site Agent', link: '/architecture/site-agent' },
          { text: 'Conventions', link: '/architecture/conventions' },
        ],
      },
      {
        text: 'Decisions (ADR)',
        collapsed: true,
        items: [
          { text: 'All decisions', link: '/architecture/decisions/' },
          { text: '0001 — Modular monolith', link: '/architecture/decisions/0001-modular-monolith' },
          { text: '0002 — Own fiscal engine', link: '/architecture/decisions/0002-own-fiscal-engine' },
          { text: '0003 — Site Agent', link: '/architecture/decisions/0003-site-agent' },
          { text: '0004 — React PWA, not Blazor', link: '/architecture/decisions/0004-react-pwa-not-blazor' },
          { text: '0005 — Plain Guid ids', link: '/architecture/decisions/0005-plain-guid-ids' },
          { text: '0006 — No MediatR', link: '/architecture/decisions/0006-no-mediatr' },
          { text: '0007 — Client-agnostic API', link: '/architecture/decisions/0007-client-agnostic-api' },
          { text: '0008 — Token auth, no cookies', link: '/architecture/decisions/0008-token-auth-no-cookies' },
          { text: '0009 — Incremental delivery', link: '/architecture/decisions/0009-incremental-delivery' },
          { text: '0010 — RLS runtime role split', link: '/architecture/decisions/0010-rls-runtime-role-split' },
          { text: '0011 — i18n: cookie + mobile seam', link: '/architecture/decisions/0011-i18n' },
        ],
      },
      {
        text: 'Fiscal',
        collapsed: false,
        items: [
          { text: 'Portuguese fiscalisation', link: '/fiscal/' },
          { text: 'AT certification', link: '/fiscal/certification' },
          { text: 'Key management', link: '/fiscal/key-management' },
        ],
      },
      {
        text: 'Development',
        collapsed: false,
        items: [
          { text: 'Getting started', link: '/development/getting-started' },
          { text: 'Testing', link: '/development/testing' },
          { text: 'End-to-end testing', link: '/development/e2e-testing' },
          { text: 'Documentation contract', link: '/development/documentation' },
          { text: 'OpenAPI document', link: '/openapi/' },
        ],
      },
      {
        text: 'Product',
        collapsed: false,
        items: [
          { text: 'Roadmap', link: '/product/roadmap' },
          { text: 'Backlog & progress', link: '/product/backlog' },
          { text: 'Differentiation', link: '/product/differentiation' },
          { text: 'Feature docs', link: '/features/' },
          { text: 'Build status', link: '/product/status' },
          { text: 'Plan & roadmap', link: '/product/plan' },
        ],
      },
      {
        text: 'Feature docs',
        collapsed: true,
        items: [
          { text: 'Discounts', link: '/features/discounts' },
          { text: 'Void a line', link: '/features/void-a-line' },
          { text: 'Menu item classification', link: '/features/menu-item-classification' },
        ],
      },
    ],

    socialLinks: [{ icon: 'github', link: 'https://github.com/mahbub9/Brasa' }],

    editLink: {
      pattern: 'https://github.com/mahbub9/Brasa/edit/main/docs/:path',
      text: 'Edit this page on GitHub',
    },

    search: { provider: 'local' },

    outline: { level: [2, 3] },

    footer: {
      message:
        'Documentation is versioned with the source and reviewed in the same pull request.',
      copyright: 'Brasa — proprietary. All rights reserved.',
    },
  },

  // Links into source and tests are absolute GitHub URLs on purpose: the code
  // is not part of this site, and a relative path would 404 here while working
  // fine in the repository. Anything still relative and unresolved is a genuine
  // broken link and should fail the build.
  ignoreDeadLinks: false,
})
