// Portugal is the primary market — this is the default language, not a
// translation of the English copy. See docs/architecture/decisions/0011-i18n.md.
export const pt = {
  app: {
    tagline: 'Retaguarda',
    loading: 'A carregar…',
  },
  nav: {
    overview: 'Visão geral',
    menu: 'Menu',
    floor: 'Plano de sala',
    staff: 'Equipa',
    comingSoon: 'Brevemente',
  },
  overview: {
    categories: 'Categorias de menu',
    items: 'Itens no menu',
    // No real plural handling, same call as pos's order.covers — see
    // docs/architecture/decisions/0011-i18n.md's "Bad" section.
    itemsUnavailable: '{{count}} indisponíveis',
    rooms: 'Salas',
    tables: 'Mesas',
    tablesOccupied: '{{count}} ocupadas',
  },
  error: {
    generic: 'Ocorreu um erro.',
  },
  language: {
    label: 'Idioma',
  },
};
