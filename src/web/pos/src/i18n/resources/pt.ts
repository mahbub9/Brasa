// Portugal is the primary market — this is the default language, not a
// translation of the English copy. See docs/architecture/decisions/0011-i18n.md.
export const pt = {
  app: {
    tagline: 'POS — protótipo inicial (I0)',
  },
  openTable: {
    title: 'Abrir mesa',
    tableLabel: 'Mesa',
    tablePlaceholder: 'ex. Mesa 12',
    covers: 'Pessoas',
    submit: 'Abrir mesa',
    submitBusy: 'A abrir…',
  },
  menu: {
    empty: 'Sem itens de menu disponíveis.',
  },
  order: {
    covers: '{{count}} pessoas',
    empty: 'Ainda sem itens — toque no menu para adicionar.',
    total: 'Total',
    split: 'Dividir',
    splitWays: 'vezes',
    previewSplit: 'Pré-visualizar divisão',
    close: 'Fechar e emitir recibo',
    closing: 'A fechar…',
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
  },
  language: {
    label: 'Idioma',
  },
};
