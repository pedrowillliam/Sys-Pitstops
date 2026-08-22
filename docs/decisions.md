# Registro de decisões

Toda decisão que foi discutida e fechada entra aqui — mesmo as pequenas. O
objetivo é evitar rediscussão: quando alguém perguntar "por que não fizemos
X?", a resposta está neste arquivo.

**Formato:** data, decisão, alternativas descartadas, motivo. Três a quatro
linhas por entrada. Entradas novas vão no fim.

Decisão revista não é apagada: adiciona-se uma nova entrada marcada como
*Revisa D-xx*, e a original ganha a marcação *Revisada por D-yy*.

---

## D-01 — Escopo reduzido ao fluxo principal
**Data:** 2026-08-10
**Decisão:** o MVP cobre ciclo da OS, Kanban, área do mecânico, orçamento com
aprovação, estoque simples, dashboard e PWA instalável. Fiscal, pagamento, RH,
ponto, comissões, chatbot com IA, código de barras e marketplace ficam fora.
**Alternativas:** entregar os seis módulos originais parcialmente.
**Motivo:** prazo real de 4 semanas com 3 pessoas. Um fluxo completo e sólido
vale mais, técnica e academicamente, do que seis módulos pela metade.

## D-02 — Instalação única em vez de multi-tenant
**Data:** 2026-08-10
**Decisão:** o sistema roda para uma oficina. Todas as tabelas raiz carregam
`workshop_id`, sempre com valor 1, mas não há RLS, troca de contexto nem tela
de oficina.
**Alternativas:** multi-tenant completo com RLS desde o início.
**Motivo:** multi-tenant só se justifica após validação. A coluna custa quase
nada agora e evita refazer o schema na Fase 2.

## D-03 — Domínio permanece especializado em oficina
**Data:** 2026-08-10
**Decisão:** manter o domínio de oficina mecânica (OS, pátio, veículo, laudo).
**Alternativas:** generalizar para "comércio local".
**Motivo:** a especialização é o diferencial do produto. Generalizar aumentaria
o trabalho, porque exigiria abstrair todo o modelo, e eliminaria o vínculo com
a Observação Direta que originou o projeto.

## D-04 — React + Vite em vez de Next.js
**Data:** 2026-08-10
**Decisão:** front-end como SPA em React + Vite + TypeScript, empacotado como
PWA.
**Alternativas:** Next.js.
**Motivo:** o back-end é ASP.NET Core, então os recursos de servidor do Next
seriam redundantes. A aplicação é integralmente autenticada, sem requisito de
SEO, e o comportamento offline depende de um shell estático que o service
worker sirva sem rede — o que a SPA entrega naturalmente.

## D-05 — Monorepo
**Data:** 2026-08-10
**Decisão:** repositório único com `/backend`, `/frontend` e `/docs`.
**Alternativas:** repositórios separados para API e front.
**Motivo:** com 3 pessoas, um PR fecha a funcionalidade inteira (endpoint e
tela), e a issue não fica dividida entre dois lugares.

## D-06 — Idioma do código
**Data:** 2026-08-10
**Decisão:** código, banco, rotas e commits em inglês; interface e documentação
em português.
**Alternativas:** tudo em português.
**Motivo:** evita a mistura de idiomas na mesma classe, que é o padrão comum em
projeto acadêmico e prejudica a leitura.

## D-07 — Preço congelado no item da OS
**Data:** 2026-08-10
**Decisão:** `service_order_items` grava `unit_price` e `description` no momento
da inserção. `part_id` serve apenas para rastreabilidade e baixa de estoque.
**Alternativas:** ler o preço vigente de `parts` nas consultas.
**Motivo:** reajuste de preço de peça reescreveria o faturamento histórico e o
dashboard passaria a mostrar valores diferentes dos orçamentos aprovados.

## D-08 — Cliente congelado na OS
**Data:** 2026-08-10
**Decisão:** `service_orders.customer_id` guarda quem era o dono no atendimento;
`vehicles.owner_id` guarda o dono atual.
**Alternativas:** resolver o cliente sempre pelo veículo.
**Motivo:** venda de veículo reescreveria o histórico. O histórico do veículo
atravessa donos (útil ao diagnóstico); o do cliente não muda.

## D-09 — Totais calculados, não armazenados
**Data:** 2026-08-10
**Decisão:** a OS não tem coluna de total. O valor é calculado por consulta. A
exceção é `quotes.total_amount`, que é armazenado.
**Alternativas:** manter total denormalizado na OS.
**Motivo:** total armazenado exige atualização em cada alteração de item, e um
caminho esquecido gera divergência silenciosa. No orçamento o valor não é
cálculo, é registro do que o cliente aceitou.

## D-10 — Orçamento em tabela própria com snapshot em JSONB
**Data:** 2026-08-10
**Decisão:** tabela `quotes` com `items_snapshot` em JSONB, `public_token` e
`expires_at`. Novo envio gera novo registro.
**Alternativas:** colunas de aprovação direto na OS; tabela `quote_items`
espelhando os itens.
**Motivo:** dá versionamento e prova do aceite sem duplicar a estrutura de
itens.

## D-11 — Baixa de estoque na conclusão, sem reserva
**Data:** 2026-08-10
**Decisão:** os `stock_movements` do tipo `OUT` e a atualização de
`quantity_on_hand` ocorrem na transição para `READY`, na mesma transação.
**Alternativas:** reserva na requisição da peça, com estados "reservada" e
"entregue".
**Motivo:** simplicidade. **Risco aceito:** uma peça pode ser prometida a duas
OS simultâneas. A reserva entra na Fase 2.

## D-12 — Um mecânico por OS
**Data:** 2026-08-10
**Decisão:** `service_orders.mechanic_id` aponta para um único responsável.
**Alternativas:** relação N:N entre OS e mecânicos.
**Motivo:** cobre o caso comum e simplifica o KPI de produtividade por
funcionário.

## D-13 — Execução exige orçamento aprovado, com dispensa registrada
**Data:** 2026-08-10
**Decisão:** a OS só entra em `IN_PROGRESS` com orçamento aprovado. O admin pode
dispensar, preenchendo `approval_waived_note`, o que também gera linha em
`service_order_status_history`.
**Alternativas:** bloqueio absoluto; ou nenhuma exigência.
**Motivo:** bloqueio absoluto não corresponde à prática da oficina em serviço
pequeno resolvido na hora; a dispensa registrada preserva a rastreabilidade.

## D-14 — Link do orçamento expira em 7 dias
**Data:** 2026-08-10
**Decisão:** após `expires_at`, o orçamento passa a `EXPIRED` e o reenvio cria
um novo registro.
**Alternativas:** link permanente.
**Motivo:** o token dá acesso a dados pessoais sem autenticação; validade
limitada reduz a exposição.

## D-15 — WhatsApp sem API oficial no MVP
**Data:** 2026-08-10
**Decisão:** o envio ao cliente é um botão que abre `wa.me` com a mensagem e o
link pré-preenchidos. O atendente confirma o envio.
**Alternativas:** integração com a Cloud API da Meta.
**Motivo:** a API exige conta business aprovada, com prazo externo à equipe. O
comportamento visível ao usuário é praticamente o mesmo.

## D-16 — PWA com cache de leitura, sem sincronização bidirecional
**Data:** 2026-08-10
**Decisão:** service worker com precache do shell e cache das consultas. Sem
fila de escrita offline.
**Alternativas:** offline-first completo com fila e resolução de conflito.
**Motivo:** sincronização bidirecional é o item mais caro do projeto e não cabe
em 4 semanas. Fica como Fase 2.

## D-17 — Contrato de API definido antes da implementação
**Data:** 2026-08-10
**Decisão:** o Swagger gerado pelo ASP.NET é a fonte do contrato, e o cliente
TypeScript do front é gerado a partir dele. Tipos não são escritos à mão.
**Alternativas:** tipos duplicados manualmente nos dois lados.
**Motivo:** front e back são pessoas diferentes trabalhando em paralelo;
divergência de tipos é a maior fonte de retrabalho nesse arranjo.
