# Modelo de dados — Sys Pitstops (MVP)

Este documento explica **por que** o banco está modelado assim. O DDL de
referência está em `docs/schema.sql`; a fonte de verdade são as migrations
do EF Core em `backend/`.

Convenção: código e banco em inglês, interface em português.

---

## 1. Decisões que não podem ser revertidas barato

### 1.1. Preço é congelado no item, não referenciado

`service_order_items.unit_price` e `service_order_items.description` guardam o
valor **no momento em que o item foi adicionado à OS**. O `part_id` existe
apenas para rastreabilidade e para a baixa de estoque — nunca para exibir preço.

Consequência prática: relatórios e dashboards **jamais** fazem
`JOIN parts ... SELECT parts.sale_price`. Se fizerem, o reajuste de preço de uma
peça reescreve o faturamento de meses anteriores.

O mesmo vale para `description`: se a peça for renomeada, a OS antiga continua
mostrando o nome que constava no orçamento aprovado.

### 1.2. O cliente é congelado na OS

`vehicles.owner_id` é o dono **atual** do veículo.
`service_orders.customer_id` é o dono **naquele atendimento**.

O histórico do veículo, portanto, atravessa donos: `WHERE vehicle_id = X` traz
todas as intervenções desde sempre, o que é exatamente o que o mecânico precisa
para diagnosticar. Já o histórico do cliente usa `customer_id`, e não muda
quando ele vende o carro.

Tabela de histórico de propriedade (`vehicle_ownerships`) fica para a Fase 2. O
snapshot na OS já resolve o problema real do MVP.

### 1.3. Tipos

| Dado | Postgres | C# |
|---|---|---|
| Dinheiro | `numeric(12,2)` | `decimal` |
| Quantidade | `numeric(10,3)` | `decimal` |
| Data e hora | `timestamptz` (sempre UTC) | `DateTimeOffset` |
| Identificador | `uuid` | `Guid` |

Nunca `float` ou `double` para dinheiro. Conversão de fuso acontece só na
exibição — se gravar em horário local, o dashboard "por período" começa a errar
nas viradas de dia.

### 1.4. `workshop_id` presente, multi-tenant ausente

Todas as tabelas raiz carregam `workshop_id`, sempre com valor `1` no MVP. Não
há RLS, não há troca de contexto, não há tela de oficina. É apenas a garantia de
que a evolução para SaaS não exige refazer o schema.

---

## 2. Totais são calculados, não armazenados

A OS **não** tem coluna `total_amount`. O total é
`sum(quantity * unit_price) - discount_amount`, calculado na consulta.

Motivo: total armazenado exige manutenção em cada inserção, edição e remoção de
item, e qualquer caminho esquecido gera divergência silenciosa entre o que a
tela mostra e o que o dashboard soma. No volume de uma oficina, o custo de
calcular é irrelevante.

A exceção é `quotes.total_amount`, que **é** armazenado — ali o valor não é um
cálculo, é o registro do que o cliente viu e aceitou.

---

## 3. Orçamento com snapshot em JSONB

`quotes.items_snapshot` guarda os itens exatamente como foram enviados. Se o
orçamento for recusado e a oficina montar outro, nasce um novo registro em
`quotes` — o anterior permanece, com o motivo da recusa.

Isso dá versionamento e prova do aceite sem criar uma tabela `quote_items`
espelhando `service_order_items`.

O acesso do cliente é por `public_token` (aleatório, com `expires_at`), sem
cadastro e sem login.

---

## 4. Estoque: movimento é a verdade

`parts.quantity_on_hand` é um valor mantido pela aplicação para leitura rápida.
A verdade auditável é `stock_movements`.

Regra: **nenhuma alteração de `quantity_on_hand` acontece sem uma linha
correspondente em `stock_movements`, dentro da mesma transação.** Se essa regra
for quebrada uma vez, o histórico de movimentação deixa de fechar com o saldo e
não há como reconciliar depois.

`quantity` é sempre positivo; a direção vem de `movement_type`
(`IN`, `OUT`, `ADJUSTMENT`).

---

## 5. Regras de negócio decididas

Estas eram as perguntas em aberto. Decisões do MVP:

**Transições de status**

```
REQUESTED -> CONFIRMED -> IN_YARD -> AWAITING_APPROVAL -> IN_PROGRESS -> READY -> DELIVERED
```

`CANCELED` é alcançável a partir de qualquer estado anterior a `READY`.

| Transição | Quem pode |
|---|---|
| `REQUESTED` → `CONFIRMED` | atendente, admin |
| `CONFIRMED` → `IN_YARD` | atendente, admin |
| `IN_YARD` → `AWAITING_APPROVAL` | mecânico, atendente, admin |
| `AWAITING_APPROVAL` → `IN_PROGRESS` | automático na aprovação do orçamento |
| `IN_PROGRESS` → `READY` | mecânico responsável, admin |
| `READY` → `DELIVERED` | atendente, admin |
| qualquer → `CANCELED` | admin |

**Execução exige orçamento aprovado.** A exceção é o admin, que pode dispensar
a aprovação registrando o motivo em `approval_waived_note` — o caso real de
serviço pequeno resolvido na hora. A dispensa também gera linha em
`service_order_status_history`.

**Baixa de estoque ocorre na transição para `READY`**, em uma única transação:
gera os `stock_movements` do tipo `OUT` e atualiza `quantity_on_hand`. Não há
reserva no MVP — uma peça pode ser prometida a duas OS simultâneas, risco aceito
em troca de simplicidade. A reserva entra na Fase 2.

**Uma OS tem um mecânico responsável** (`mechanic_id`). Múltiplos mecânicos por
OS ficam para a Fase 2; o KPI de produtividade por funcionário usa esse campo.

**O link do orçamento expira em 7 dias.** Após `expires_at`, o status vira
`EXPIRED` e o atendente precisa reenviar, o que cria um novo registro em
`quotes`.

**Cancelamento não apaga nada.** A OS permanece com status `CANCELED` e sai das
consultas de faturamento.

---

## 6. KPIs e de onde saem

| Indicador | Origem |
|---|---|
| Faturamento no período | itens das OS em `READY`/`DELIVERED`, por `closed_at` |
| Ticket médio | mesma consulta, média por OS |
| OS por status | contagem em `service_orders` |
| Tempo médio de execução | `status_history`: `IN_PROGRESS` → `READY` |
| Serviços mais executados | agrupamento por `description` dos itens `SERVICE` |
| Itens abaixo do mínimo | `parts` onde `quantity_on_hand <= min_quantity` |

Nenhum KPI exige tabela nova. Note que o tempo médio de execução **só existe
porque `status_history` existe** — sem ela, esse indicador seria impossível de
calcular retroativamente.

---

## 7. Organização do repositório

```
/backend    API em C# (ASP.NET Core) e migrations
/frontend   PWA em React + Vite + TypeScript
/docs       Este documento e os demais
```

**O que vai no `README.md` da raiz:** stack, pré-requisitos, como subir o
ambiente, comandos, convenção de branch e commit, e links para `/docs`. Alvo:
duas telas. Se passar disso, ninguém lê.

**O que vai em `/docs`:**

| Arquivo | Conteúdo |
|---|---|
| `data-model.md` | este documento |
| `schema.sql` | DDL de referência |
| `decisions.md` | registro curto de decisões e o motivo de cada uma |

O contrato da API não fica em `/docs`: é o Swagger gerado pelo ASP.NET,
servido em `/swagger` assim que o backend sobe (ver D-18).

`decisions.md` é o que evita a discussão circular na semana 3. Formato: data,
decisão, alternativas descartadas, motivo. Três linhas por entrada.
