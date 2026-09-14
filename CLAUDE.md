# CLAUDE.md

Contexto para o Claude Code trabalhar neste repositório. Regras aqui têm
precedência; quando algo não estiver coberto, siga `README.md` e `docs/`.

## Projeto

Sys Pitstops — ERP em PWA para gestão de oficinas mecânicas independentes.
Projeto acadêmico (UFAPE, 2026.1), 3 pessoas, prazo curto. O MVP cobre o ciclo
completo da Ordem de Serviço (OS): entrada do veículo → orçamento → execução →
entrega.

**Estado em 2026-09-14:** Semanas 1, 2 e 3 concluídas; Semana 4 em andamento.

Prontos: schema e migrations, autenticação (JWT em cookie httpOnly), CRUD de
clientes e veículos com tela de clientes, endpoint mínimo de usuários, **a Ordem
de Serviço de ponta a ponta** — abertura, detalhe, itens com preço congelado,
transições com histórico, painel Kanban e fila do mecânico —, **o orçamento
completo** (geração a partir da OS, link público de aprovação e tela de
acompanhamento), **o estoque** (peças, movimentações e baixa automática na
conclusão), **as fotos do laudo**, a **carga de demonstração**, CI no GitHub
Actions e o sistema **publicado e no ar** em https://syspitstops.onrender.com.

Veículos têm API e entram pelo cadastro do cliente, mas não têm tela própria: o
protótipo não prevê uma.

Em aberto, para fechar a Semana 4: **dashboard** e **ajustes de PWA** — o
`theme_color` do manifest ficou na paleta antiga, de antes da conversão do
shell. Os KPIs do dashboard estão na §6 do `data-model.md` e nenhum exige tabela
nova; a D-47 corta do protótipo a nota de qualidade e a especialidade do
mecânico, que não têm origem no modelo.

Uma dívida explícita, registrada na D-45: o `IMediaStorage` tem só a
implementação em disco local, então **foto enviada em produção se perde no
deploy seguinte**. A próxima entrega de fotos é a classe do Supabase que a D-26
decidiu, mais o bucket criado no painel.

## Rodar com o sistema povoado

Um banco vazio abre sem nada, e um quadro sem cartão se lê como defeito. A carga
da D-46 cria seis meses de oficina fictícia:

```bash
SEED_DEMO=true SEED_DEMO_PASSWORD=demo1234 dotnet run --project src/SysPitstops.Api
```

Roda uma vez, e só quando o banco não tem nenhum cliente — as migrations rodam a
cada subida (D-28), então sem essa guarda cada deploy empilharia outra oficina.
Os logins saem do nome: `roberto.silva@syspitstops.local` (mecânico),
`ana.lima@syspitstops.local` (atendente). O README traz o comando para
recomeçar do zero.

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
`CONFIRMED → IN_YARD` exige mecânico responsável definido (D-38).
`AWAITING_APPROVAL → IN_PROGRESS` é automático na aprovação do orçamento (admin
pode dispensar registrando `approval_waived_note`). A **baixa de estoque ocorre
na transição para `READY`**, em transação única. Sem reserva de peça no MVP.

## Git

- Branches: `main` (protegida, publicável), `feat/<escopo>`, `fix/<escopo>`,
  `docs/<escopo>`.
- Toda mudança entra por PR com ao menos uma aprovação; review parado por 12h+
  libera merge direto. PR referencia a issue.
- A CI (`.github/workflows/ci.yml`) roda em cada PR: testes do backend, lint e
  build do front, e build da imagem de deploy.
- Só faça commit/push quando pedido.

## API e tipos

O contrato da API é o Swagger gerado pelo ASP.NET (`/swagger`). O cliente
TypeScript do front é **gerado a partir dele** — não escreva tipos de
request/response à mão nos dois lados.

Depois de mudar qualquer endpoint ou DTO, **regenere o cliente**:

```bash
# com a API rodando (dotnet run --project backend/src/SysPitstops.Api)
cd frontend && npm run generate:api
```

Se isso for esquecido, o front segue com o contrato antigo e simplesmente não
enxerga os endpoints novos — aconteceu entre os PRs #3 e #4.

## Publicação

Um **único serviço** no Render serve a API e a SPA compilada (D-26): front e API
compartilham origem, e por isso não há CORS (D-29). A migration é aplicada no
startup da aplicação (D-28). `Dockerfile` e `render.yaml` ficam na raiz.

## Design

Telas no Figma (arquivo "Sys-PitStop", fileKey `jGZ6p2AhbpQAVcy7wV6K9o`).
Estão implementadas: **Clientes** (`frontend/src/customers/`), **Kanban**,
**abertura e detalhe da OS** e **Minha fila** (`frontend/src/service-orders/`),
**Orçamento** mais a página pública de aprovação (`frontend/src/quotes/`) e
**Estoque** (`frontend/src/inventory/`). Faltam **Dashboard**, **Mecânicos** e
**Configurações**, ainda placeholder (`frontend/src/pages/Placeholder.tsx`) —
o menu chama a tela de clientes de "Clientes Inscritos", como o protótipo. O
menu e os papéis que enxergam cada item estão em
`frontend/src/layout/navigation.ts`, com a flag `ready` marcando o que já existe.

O **shell** (`frontend/src/layout/`) segue o protótipo: topo azul com o logo em
cartão branco, busca global e usuário; rail escuro flutuante de ícones à
esquerda. A paleta em `frontend/src/index.css` foi amostrada das telas do Figma
— `#173676` no topo e no botão primário, `#0f2942` no rail e na faixa de
título — e a fonte é Montserrat, como no desenho. Toda tela abre com
`PageHeader`, que é a faixa escura do protótipo.

As fotos do laudo entram por `IMediaStorage` (D-25): a rota
`GET /api/service-orders/{id}/media/{mediaId}` serve os bytes, e o
`storage_key` nunca sai do servidor (D-44). O navegador comprime antes de
enviar.

O protótipo traz campos que o modelo não tem — prioridade e tempo estimado
foram deixados de fora (D-33 e D-34), o "Editar" da tela de orçamento não
existe porque o orçamento é imutável (D-36), e a abertura de OS corta os
serviços em caixas e o valor estimado (D-37). Ao implementar uma tela nova, confira se
os campos desenhados existem no schema antes de assumir que sim.

A extração do protótipo está em `design/figma/` — PNG de 15 telas, o código de
referência do login e `estrutura-completa.xml` com a geometria e os textos das
32. Leia `design/figma/LEIA-ME.md` antes: ele traz a cobertura, como extrair o
que falta e as divergências entre protótipo e modelo de dados.

**Consulte o Figma ao implementar cada tela.** O arquivo tem bem mais frames do
que os nomes de rota sugerem — além de Kanban, Dashboard e Mecânicos, há
`Cadastro serviço`, `Cadastro Estoque`, `Cotação`, `Orçamento` e as telas
`Mobile - Mecânico`.

## Documentação de referência

| Arquivo | Conteúdo |
|---|---|
| `docs/data-model.md` | Modelo de dados, regras de negócio, o "porquê" |
| `docs/schema.sql` | DDL de referência (não é fonte de verdade) |
| `docs/decisions.md` | Registro de decisões técnicas (formato: data, decisão, alternativas, motivo) |

Decisão nova sobre algo já decidido: adicione entrada em `docs/decisions.md`
marcada como *Revisa D-xx*; não apague a original.