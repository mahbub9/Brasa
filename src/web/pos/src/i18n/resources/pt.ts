// Portugal is the primary market — this is the default language, not a
// translation of the English copy. See docs/architecture/decisions/0011-i18n.md.
export const pt = {
  app: {
    tagline: 'POS — protótipo inicial (I0)',
  },
  floor: {
    title: 'Escolher mesa',
    seats: '{{count}} lugares',
    covers: 'Pessoas',
    open: 'Abrir mesa',
    opening: 'A abrir…',
    clear: 'Limpar mesa',
    empty: 'Sem mesas configuradas.',
    newTakeaway: 'Nova venda ao balcão',
    takeawayLabelPlaceholder: 'Nome ou nº do talão (opcional)',
    takeawayDefaultLabel: 'Levantamento',
    // The visible display form of a seeded table label ("Mesa 1") — see
    // src/lib/tableLabel.ts. Table.Label itself is unchanged; only the
    // on-screen rendering goes through this.
    tableLabel: 'Mesa {{number}}',
    state: {
      Free: 'Livre',
      Occupied: 'Ocupada',
      BillRequested: 'Conta pedida',
      Dirty: 'Por limpar',
    },
  },
  menu: {
    empty: 'Sem itens de menu disponíveis.',
    containsAllergens: 'Contém',
    allergen: {
      Gluten: 'Glúten',
      Crustaceans: 'Crustáceos',
      Eggs: 'Ovos',
      Fish: 'Peixe',
      Peanuts: 'Amendoim',
      Soybeans: 'Soja',
      Milk: 'Leite',
      Nuts: 'Frutos de casca rija',
      Celery: 'Aipo',
      Mustard: 'Mostarda',
      Sesame: 'Sésamo',
      Sulphites: 'Sulfitos',
      Lupin: 'Tremoço',
      Molluscs: 'Moluscos',
    },
  },
  modifiers: {
    title: 'Personalizar',
    required: 'Obrigatório',
    chooseOne: 'Escolha 1',
    chooseUpTo: 'Escolha até {{count}}',
    chooseBetween: 'Escolha entre {{min}} e {{max}}',
    add: 'Adicionar',
    cancel: 'Cancelar',
  },
  order: {
    covers: '{{count}} pessoas',
    empty: 'Ainda sem itens — toque no menu para adicionar.',
    total: 'Total',
    split: 'Dividir',
    splitWays: 'vezes',
    previewSplit: 'Pré-visualizar divisão',
    preBill: 'Ver conta',
    requestBill: 'Pedir conta',
    close: 'Fechar e emitir recibo',
    closing: 'A fechar…',
    quantityDecrease: 'Diminuir quantidade',
    quantityIncrease: 'Aumentar quantidade',
    addNote: 'Adicionar nota',
    notesLabel: 'Nota',
    notesPlaceholder: 'ex.: sem cebola',
    notesSave: 'Guardar',
    notesCancel: 'Cancelar',
    transferTable: 'Transferir mesa',
  },
  transfer: {
    title: 'Transferir para',
    noFreeTables: 'Sem mesas livres.',
    cancel: 'Cancelar',
  },
  preBill: {
    title: 'Conta',
    notice: 'Documento não fiscal — não serve de fatura',
    net: 'Líquido',
    vat: 'IVA',
    generated: 'Gerado às',
    close: 'Fechar',
  },
  receipt: {
    title: 'Recibo emitido',
    document: 'Documento',
    atcud: 'ATCUD',
    net: 'Líquido',
    vat: 'IVA',
    gross: 'Total',
    issued: 'Emitido',
    mockNotice: 'Fornecedor fiscal fictício — este documento não tem valor legal.',
    newTable: 'Abrir outra mesa',
  },
  error: {
    dismiss: 'Fechar',
    generic: 'Ocorreu um erro.',
    // Keyed by the server's stable error code (docs/architecture/error-codes.md),
    // nested to match its dots — "floor.table_not_dirty" resolves as
    // error.code.floor.table_not_dirty, no special i18next config needed.
    // Only the codes pos's own API calls (src/api/client.ts) can actually
    // trigger are covered here; describeError() falls back to the raw
    // server message for anything else, so an untranslated code is never
    // silently hidden, just shown in English until it's added here.
    code: {
      floor: {
        table_not_dirty: 'Esta mesa não precisa de ser limpa.',
        table_not_found: 'Mesa não encontrada.',
        table_not_occupied: 'Esta mesa não está ocupada.',
        table_not_free: 'Esta mesa já está ocupada.',
      },
      order: {
        invalid_cover_count: 'Indique pelo menos 1 pessoa.',
        not_found: 'Pedido não encontrado.',
        not_open: 'Este pedido já não está aberto.',
        invalid_quantity: 'Indique uma quantidade válida.',
        notes_too_long: 'A nota é demasiado longa (máx. 300 caracteres).',
        line_not_found: 'Este item já não está no pedido.',
        line_voided: 'Este item foi cancelado e já não pode ser alterado.',
        invalid_split: 'Não foi possível dividir a conta com estes valores.',
        empty: 'Adicione pelo menos um item antes de continuar.',
        already_closed: 'Este pedido já foi fechado.',
      },
      catalog: {
        item_not_found: 'Item de menu não encontrado.',
        item_unavailable: 'Este item está indisponível de momento.',
        modifier_not_found: 'Opção de personalização não encontrada.',
        modifier_selection_invalid: 'Verifique as opções escolhidas para este item.',
      },
      fiscal: {
        no_lines: 'Não é possível emitir o recibo: não há itens para faturar.',
      },
      request: {
        idempotency_key_required: 'Ocorreu um erro. Tente novamente.',
        rate_limited: 'O sistema está ocupado. Aguarde um momento e tente novamente.',
      },
    },
  },
  language: {
    label: 'Idioma',
  },
};
