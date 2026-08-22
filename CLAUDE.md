# CLAUDE.md

Contexto para o Claude Code trabalhar neste repositório. Regras aqui têm
precedência; quando algo não estiver coberto, siga `README.md` e `docs/`.

## Projeto

Sys Pitstops — ERP em PWA para gestão de oficinas mecânicas independentes.
Projeto acadêmico (UFAPE, 2026.1), 3 pessoas, prazo curto. O MVP cobre o ciclo
completo da Ordem de Serviço (OS): entrada do veículo → orçamento → execução →
entrega.

**Fase atual:** arquitetura/design. Ainda não há código — `backend/` e
`frontend/` estão vazios. Não comece a implementar sem que isso seja pedido.

## Stack

| Camada | Tecnologia |
|---|---|
| Back-end | C# / ASP.NET Core (Web API) + Entity Framework Core |
| Front-end | React + Vite + TypeScript (PWA) + Tailwind CSS + TanStack Query |
| Banco | PostgreSQL 16 |

Estrutura: `/backend` (API, domínio, migrations), `/frontend` (PWA), `/docs`
(modelo de dados, schema, decisões).

## Idioma (importante)

- **Código, banco, nomes de rotas e mensagens de commit:** inglês.
- **Interface, documentação e corpo de PR:** português.
- Mensagens de commit seguem Conventional Commits, mas **em português** por
  decisão da equipe (ex: `docs: padroniza backend/frontend nos docs`). O tipo
  (`feat`, `fix`, `docs`...) permanece em inglês.

## Regras invioláveis

- **Dinheiro é `decimal` no C# e `numeric(12,2)` no banco. Nunca `float`/`double`.**
  Quantidade é `numeric(10,3)`. Datas são `timestamptz` sempre em UTC; conversão
  de fuso só na exibição.
- **Schema só muda via migration do EF Core.** Nunca altere o banco direto nem
  edite `docs/schema.sql` como se fosse a fonte de verdade — ele é só referência.
- **Preço e descrição são congelados** em `service_order_items` no momento da
  inserção. Nunca faça `JOIN parts` para exibir preço em relatório/dashboard —
  isso reescreveria o faturamento histórico.
- **Cliente é congelado na OS** (`service_orders.customer_id`), separado do dono
  atual do veículo (`vehicles.owner_id`). Não resolva o cliente pelo veículo.
- **Estoque:** toda alteração de `parts.quantity_on_hand` gera uma linha em
  `stock_movements` na mesma transação. `quantity` é sempre positivo; a direção
  vem de `movement_type` (`IN`/`OUT`/`ADJUSTMENT`).
- **Totais da OS são calculados, não armazenados.** A exceção é
  `quotes.total_amount`, que registra o que o cliente aceitou.
- **Multi-tenant:** todas as tabelas raiz têm `workshop_id`, sempre `1` no MVP.
  Não há RLS nem troca de contexto — mantenha a coluna, não implemente tenancy.

## Fluxo da OS (máquina de estados)

```
REQUESTED → CONFIRMED → IN_YARD → AWAITING_APPROVAL → IN_PROGRESS → READY → DELIVERED
```

`CANCELED` é alcançável de qualquer estado anterior a `READY`.
`AWAITING_APPROVAL → IN_PROGRESS` é automático na aprovação do orçamento (admin
pode dispensar registrando `approval_waived_note`). A **baixa de estoque ocorre
na transição para `READY`**, em transação única. Sem reserva de peça no MVP.

## Git

- Branches: `main` (protegida, publicável), `feat/<escopo>`, `fix/<escopo>`,
  `docs/<escopo>`. Na fase de arquitetura, commit direto na `main` é aceitável.
- Toda mudança relevante entra por PR com ao menos uma aprovação; review parado
  por 12h+ libera merge direto. PR referencia a issue.
- Só faça commit/push quando pedido.

## API e tipos

O contrato da API é o Swagger gerado pelo ASP.NET (`/swagger`). O cliente
TypeScript do front é **gerado a partir dele** — não escreva tipos de
request/response à mão nos dois lados.

## Design

Telas no Figma (arquivo "Sys-PitStop", fileKey `jGZ6p2AhbpQAVcy7wV6K9o`).
Mapear todas as telas só quando for implementar.

## Documentação de referência

| Arquivo | Conteúdo |
|---|---|
| `docs/data-model.md` | Modelo de dados, regras de negócio, o "porquê" |
| `docs/schema.sql` | DDL de referência (não é fonte de verdade) |
| `docs/decisions.md` | Registro de decisões técnicas (formato: data, decisão, alternativas, motivo) |

Decisão nova sobre algo já decidido: adicione entrada em `docs/decisions.md`
marcada como *Revisa D-xx*; não apague a original.