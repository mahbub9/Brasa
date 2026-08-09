// Plain English words for every piece of UI chrome — not a token gesture.
// Staff here are not all Portuguese speakers (many are Bangladeshi workers
// in Portugal), so this must actually read as English, not decorate a
// Portuguese-first screen with a language switch that doesn't change much.
// Menu item/category names stay untranslated (real tenant data, see
// docs/architecture/decisions/0011-i18n.md) — everything else here is UI
// copy and must be genuinely English.
export const en = {
  app: {
    tagline: 'Back office',
    loading: 'Loading…',
  },
  nav: {
    overview: 'Overview',
    menu: 'Menu',
    floor: 'Floor plan',
    staff: 'Staff',
    comingSoon: 'Coming soon',
  },
  overview: {
    categories: 'Menu categories',
    categoriesHidden: '{{count}} hidden',
    items: 'Menu items',
    itemsUnavailable: '{{count}} unavailable',
    rooms: 'Rooms',
    tables: 'Tables',
    tablesOccupied: '{{count}} occupied',
  },
  menu: {
    empty: 'No menu categories yet.',
    noItems: 'No items in this category.',
    visible: 'Visible',
    hidden: 'Hidden',
    hide: 'Hide',
    show: 'Show',
    available: 'Available',
    unavailable: 'Unavailable',
    markAvailable: 'Mark available',
    markUnavailable: 'Mark unavailable',
    editPrice: 'Edit price',
    takeaway: 'Takeaway',
    sameAsDineIn: 'Same as dine-in',
    addTakeawayPrice: 'Add takeaway price',
    deleteItem: 'Delete',
    deleteConfirm: 'Confirm delete?',
    deleted: 'Deleted.',
    importTitle: 'Import items (CSV)',
    importButton: 'Import',
    importChoose: 'Choose file…',
    importCreated: '{{count}} items created.',
    importRowError: 'Row {{row}}: {{message}}',
  },
  common: {
    save: 'Save',
    cancel: 'Cancel',
    clear: 'Clear',
    yes: 'Yes',
    no: 'No',
  },
  error: {
    generic: 'Something went wrong.',
  },
  language: {
    label: 'Language',
  },
};
