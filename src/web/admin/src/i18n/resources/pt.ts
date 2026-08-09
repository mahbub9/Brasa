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
    // No real plural handling, same call as pos's order.covers — see
    // docs/architecture/decisions/0011-i18n.md's "Bad" section.
    categoriesHidden: '{{count}} ocultas',
    items: 'Itens no menu',
    itemsUnavailable: '{{count}} indisponíveis',
    rooms: 'Salas',
    tables: 'Mesas',
    tablesOccupied: '{{count}} ocupadas',
  },
  menu: {
    empty: 'Ainda sem categorias de menu.',
    noItems: 'Sem itens nesta categoria.',
    visible: 'Visível',
    hidden: 'Oculta',
    hide: 'Ocultar',
    show: 'Mostrar',
    available: 'Disponível',
    unavailable: 'Indisponível',
    markAvailable: 'Disponibilizar',
    markUnavailable: 'Indisponibilizar',
    editPrice: 'Editar preço',
    takeaway: 'Levantamento',
    sameAsDineIn: 'Igual ao consumo local',
    addTakeawayPrice: 'Adicionar preço de levantamento',
    deleteItem: 'Eliminar',
    deleteConfirm: 'Confirmar eliminação?',
    deleted: 'Eliminado.',
    importTitle: 'Importar itens (CSV)',
    importButton: 'Importar',
    importChoose: 'Escolher ficheiro…',
    importCreated: '{{count}} itens criados.',
    importRowError: 'Linha {{row}}: {{message}}',
  },
  common: {
    save: 'Guardar',
    cancel: 'Cancelar',
    clear: 'Limpar',
    yes: 'Sim',
    no: 'Não',
  },
  error: {
    generic: 'Ocorreu um erro.',
  },
  language: {
    label: 'Idioma',
  },
};
