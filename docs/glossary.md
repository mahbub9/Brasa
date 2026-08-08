# Glossary

Portuguese fiscal, tax and restaurant terminology used throughout this codebase
and in conversations with accountants, AT, and customers.

## Fiscal and tax

| Term | Meaning |
|---|---|
| **AT** | *Autoridade Tributária e Aduaneira* — the Portuguese tax authority. Certifies invoicing software and receives SAF-T submissions |
| **ATCUD** | *Código Único de Documento*. `{SeriesValidationCode}-{SequentialNumber}`, printed on every fiscal document. The validation code comes from registering the series with AT |
| **Certificação** | Prior certification of invoicing software by AT under Portaria 363/2010. Granted to the **producer** |
| **CIRC** | *Código do Imposto sobre o Rendimento das Pessoas Coletivas* — corporate income tax code. Art. 123 is the basis for the certification requirement |
| **Fatura** (`FT`) | Full invoice. Used when the customer supplies a NIF and wants a complete invoice |
| **Fatura simplificada** (`FS`) | Simplified invoice. Permitted to final consumers up to €1,000. **The restaurant workhorse** — most table payments produce one |
| **Fatura-recibo** (`FR`) | Combined invoice and receipt |
| **Nota de crédito** (`NC`) | Credit note. **The only lawful way to correct an issued document** |
| **Documento não fiscal** | Non-fiscal document. The pre-bill handed to a table (*a conta*) is one. Issuing it as an invoice would fiscalise every table that merely asks to see the bill |
| **IES** | *Informação Empresarial Simplificada* — annual business filing. Accounting SAF-T is being folded into it |
| **IVA** | *Imposto sobre o Valor Acrescentado* — VAT |
| **Taxa reduzida / intermédia / normal** | The three VAT bands. Mainland 2026: 6% / 13% / 23%. Madeira and the Azores use different rates |
| **Modelo 24** | The form used to request software certification from AT (per Declaração 169/2010) |
| **NIF** | *Número de Identificação Fiscal* — tax identification number, for both businesses and individuals |
| **Portaria** | A ministerial regulation. **363/2010** governs software certification; **340/2013** amended it |
| **QES** | Qualified Electronic Signature. From 2026, PDF invoices emailed to customers are only valid with one |
| **SAF-T (PT)** | *Standard Audit File for Tax*. XML export of invoicing or accounting data. Invoicing SAF-T is submitted monthly, **by the 5th** |
| **Série** | Document series. Numbering and the signature chain are **per-series**, never global. Each is registered with AT independently |
| **Situação tributária regularizada** | Being in good standing with the tax authority. A prerequisite for applying for certification |

## Restaurant operations

| Term | Meaning |
|---|---|
| **Abertura de caixa** | Opening the cash drawer / starting a cash session, with a counted float |
| **Fecho de caixa** | Closing the cash session; counting and reconciling against recorded takings |
| **Conta** | The bill presented to a table before payment. A *documento não fiscal* |
| **Cover** | One diner. A table of four is four covers |
| **Course** | A stage of a meal (starter, main, dessert). "Course firing" is releasing the next stage to the kitchen at the right moment |
| **Bump** | Marking an order or item complete on the kitchen display |
| **KDS** | Kitchen Display System — a screen replacing or supplementing paper kitchen tickets |
| **Prato do dia** | Dish of the day |
| **TPA** | *Terminal de Pagamento Automático* — a card payment terminal |
| **Multibanco** | The Portuguese interbank network. Also the ATM/reference payment method |
| **MB WAY** | Mobile payment on the Multibanco network, widely used in Portugal |
| **SIBS** | The company operating Multibanco and MB WAY |
| **Takeaway / comida para levar** | Food sold for consumption off-premises. **Historically taxed differently from dine-in — verify current rules** |
| **X report** | Mid-shift sales snapshot. Does **not** close the session |
| **Z report** | End-of-day close. Finalises the session; must reconcile with the fiscal documents to the cent |

## Technical terms as used here

| Term | Meaning |
|---|---|
| **Business day** | The trading day, which rolls over at a configured local hour (typically 04:00–06:00), not at midnight. A sale rung at 02:00 belongs to the previous trading day. Used for the Z report, **never** for dating fiscal documents |
| **ESC/POS** | The Epson-originated command language most thermal receipt printers speak |
| **Golden file** | A committed expected-output fixture. A diff in a fiscal golden file is either a bug or a change requiring AT notification — never a casual update |
| **Outbox** | Events written in the same transaction as the state change that produced them, delivered afterwards. Here it is both the cross-module channel and the offline sync mechanism |
| **RLS** | PostgreSQL Row-Level Security. The **real** tenant isolation boundary; EF query filters are only a convenience |
| **Signature chain** | Each fiscal document is signed over a string that includes the previous document's hash **in the same series**, making tampering evident |
| **Site** | One restaurant location. A tenant (organization) may have many |
| **Site Agent** | The .NET worker running inside a restaurant. Holds the signing key, drives printers, serves the LAN |
| **Terminal** | A POS device, or the Site Agent acting as one |
| **Tenant** | The customer organization. The isolation boundary — data never crosses it |

## Regions

| Region | IANA timezone | Note |
|---|---|---|
| Continental | `Europe/Lisbon` | Mainland Portugal |
| Madeira | `Atlantic/Madeira` | Same clock as the mainland, different VAT rates |
| Açores (Azores) | `Atlantic/Azores` | **One hour behind the mainland.** Moves daily close and SAF-T period boundaries |
