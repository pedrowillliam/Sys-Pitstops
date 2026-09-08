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

## D-18 — Backend primeiro, sem documento de contrato à parte
**Data:** 2026-08-22
**Decisão:** o backend é construído primeiro e publica as rotas com DTOs desde
os primeiros dias, ainda sem lógica. Assim o Swagger existe cedo e o front gera
o cliente TypeScript contra ele. Não haverá `docs/api-contract.md`.
**Alternativas:** escrever `api-contract.md` como acordo prévio entre front e
back, enquanto o Swagger não existisse.
**Motivo:** complementa D-17. Um contrato em Markdown desatualiza em relação ao
código, que é exatamente a divergência que D-17 quer evitar. Publicar as rotas
cedo destrava o front sem criar uma segunda fonte de verdade.

## D-19 — Autenticação com JWT próprio e BCrypt, sem refresh token
**Data:** 2026-08-22
**Decisão:** autenticação própria com JWT assinado e senha em BCrypt, sem
ASP.NET Identity. Access token de 8 horas — um turno de trabalho — e nenhum
refresh token. A revogação é feita por `users.is_active`, verificado no banco a
cada requisição.
**Alternativas:** ASP.NET Identity; access token curto com refresh token em
tabela própria.
**Motivo:** o schema já tem `users` com `role` como enum, e o Identity traria o
esquema dele. O refresh token custa um interceptor no front que não se paga em
4 semanas. **Risco aceito:** um token roubado vale até 8 horas. O refresh entra
na Fase 2.

## D-20 — Token em cookie httpOnly, não em localStorage
**Data:** 2026-08-22
**Decisão:** o access token trafega em cookie `httpOnly` com `SameSite=Lax`. Em
desenvolvimento, o Vite faz proxy de `/api` para o backend, de modo que front e
API sejam a mesma origem.
**Alternativas:** guardar o token em `localStorage`.
**Motivo:** no `localStorage`, basta um script injetado para o token vazar; o
cookie `httpOnly` não é legível por JavaScript. O CSRF que o cookie abriria é
coberto por `SameSite=Lax`, e o proxy do Vite elimina o atrito de CORS em dev.

## D-21 — Rota pública do orçamento: anônima, com rate limit e token forte
**Data:** 2026-08-22
**Decisão:** a rota de consulta e aprovação do orçamento fica fora do filtro de
autorização, com rate limit por IP. O `public_token` tem 32 bytes gerados por
`RandomNumberGenerator` — nunca `Random`, nunca sequencial.
**Alternativas:** exigir algum cadastro do cliente; confiar apenas no sigilo do
token, sem limite de tentativas.
**Motivo:** D-10 e D-15 exigem que o cliente abra o link sem login, o que faz
dessa a única superfície sem autenticação do sistema. Sem limite, dá para varrer
tokens até achar um válido — e ele não só expõe dado pessoal como permite
aprovar a OS, disparando `AWAITING_APPROVAL -> IN_PROGRESS`.

## D-22 — Primeiro admin criado por seed no startup
**Data:** 2026-08-22
**Decisão:** na subida, a aplicação cria um usuário `ADMIN` se não houver nenhum,
usando `SEED_ADMIN_EMAIL` e `SEED_ADMIN_PASSWORD` vindos do ambiente. O seed é
idempotente: havendo admin, não faz nada e nunca sobrescreve senha.
**Alternativas:** comando de CLI dedicado; endpoint de setup na primeira execução.
**Motivo:** sem isso não existe primeiro login. O comando de CLI vira o passo que
todo mundo esquece ao montar o ambiente, e senha não pode ficar no código.

## D-23 — Estoque pode ficar negativo
**Data:** 2026-08-22
**Decisão:** a baixa na transição para `READY` não bloqueia por falta de saldo.
O `stock_movements` do tipo `OUT` é gravado e `quantity_on_hand` pode ficar
negativo. A tela avisa ao adicionar peça sem saldo, e a correção se dá por um
movimento `ADJUSTMENT`.
**Alternativas:** bloquear a transição; bloquear com dispensa registrada, nos
moldes de D-13.
**Motivo:** quando a OS fica pronta, a peça já foi fisicamente usada. Recusar o
registro deixaria `stock_movements` incompleto, contrariando o §4 do
`data-model.md`, que o define como a verdade auditável. Saldo negativo indica
cadastro desatualizado, não dado corrompido — é o sintoma visível do risco que
D-11 já aceitou.

## D-24 — Testes cobrem domínio e CRUD, sem ponta a ponta
**Data:** 2026-08-22
**Decisão:** xUnit cobrindo o núcleo de domínio — máquina de estados com permissão
por papel, baixa de estoque em transação única e cálculo do total com preço
congelado — e também os CRUD de clientes, veículos e peças, com atenção às
validações e às restrições de unicidade (placa, SKU). Sem testes de ponta a ponta
e sem testes de front-end.
**Alternativas:** nenhum teste; cobertura ampla incluindo e2e e front.
**Motivo:** deixa explícito na revisão de PR o que é obrigatório, em vez de virar
julgamento caso a caso. E2e e teste de front não cabem em 4 semanas.

## D-25 — Fotos atrás de uma interface de storage, começando em disco local
**Data:** 2026-08-22
*Revisada por D-26*
**Decisão:** o upload passa por uma abstração `IMediaStorage`, com implementação
em disco local no MVP. `service_order_media.storage_key` guarda a chave lógica do
arquivo, nunca um caminho absoluto. A imagem é comprimida no navegador antes do
envio, e o servidor valida tipo e tamanho.
**Alternativas:** gravar direto numa pasta do servidor, sem abstração; MinIO no
docker-compose; storage em nuvem desde já.
**Motivo:** o destino definitivo depende da decisão de publicação, ainda aberta —
e disco de plano gratuito é efêmero, apagaria as fotos a cada deploy. A interface
desacopla as duas decisões: trocar o destino vira uma classe nova. A compressão
no cliente é o que torna o upload viável no pátio com sinal fraco.

## D-26 — Publicação no Render como serviço único; fotos no Supabase Storage
**Data:** 2026-08-22
*Revisa D-25*
**Decisão:** a aplicação é publicada no Render como **um único serviço**, com o
ASP.NET servindo a SPA já compilada, mais o Postgres gerenciado do Render. As
fotos vão para o Supabase Storage pelo endpoint compatível com S3, em bucket
privado, exibidas por URL assinada de curta duração. Em desenvolvimento o
`IMediaStorage` continua gravando em disco local.
**Alternativas:** dois serviços separados no Render, um para o front e outro para
a API; disco local também em produção; Cloudflare R2.
**Motivo:** dois serviços colocariam front e API em sites diferentes, e o cookie
`SameSite=Lax` da D-20 deixaria de ser enviado — o login passaria local e
falharia em produção. Serviço único preserva a mesma origem que o proxy do Vite
já garante em dev, e dispensa CORS. O disco do plano gratuito é efêmero e
apagaria as fotos a cada deploy; o endpoint S3 mantém a portabilidade que a D-25
comprou, permitindo trocar de provedor por configuração.

## D-27 — Ordem dos rótulos de enum é alfabética, não a do fluxo
**Data:** 2026-08-22
**Decisão:** os tipos enum criados pela migration têm os rótulos em ordem
alfabética, e não na ordem declarada em `docs/schema.sql`. A ordem do fluxo da OS
é responsabilidade da aplicação — o Kanban define a sequência das colunas no
front, e qualquer consulta que precise dessa ordem usa `CASE` explícito.
**Consequência a lembrar:** `ORDER BY status` **não** devolve
`REQUESTED, CONFIRMED, IN_YARD, ...`; devolve ordem alfabética.
**Alternativas:** forçar a ordem de declaração com SQL manual numa migration.
**Motivo:** a ordem só afeta `ORDER BY` e operadores de comparação; o EF grava e
lê pelo rótulo, então nada quebra. Forçar a ordem exigiria editar à mão o SQL que
o Npgsql gera, o que é frágil e teria de ser repetido a cada mudança de enum.

## D-28 — Migration aplicada na subida da aplicação
**Data:** 2026-09-03
**Decisão:** a aplicação chama `Database.MigrateAsync()` no startup, antes do
seed do admin. Não há passo de release separado no deploy.
**Alternativas:** `preDeployCommand` no Render rodando `dotnet ef database
update`; aplicar a migration à mão a cada publicação.
**Motivo:** o `preDeployCommand` do Render é recurso de plano pago, e a
aplicação manual é o passo que alguém esquece — e aí o container sobe contra um
schema velho e quebra em produção, não no deploy. Continua valendo a regra do
`CLAUDE.md`: o schema só muda por migration do EF Core; isto só define *quando*
ela roda. **Risco aceito:** com mais de uma instância, duas subidas simultâneas
disputariam a migration. O MVP roda uma instância só; quando houver mais, isto
vira um passo de release.

## D-29 — Sem CORS, porque não há origem cruzada
**Data:** 2026-09-03
**Decisão:** o backend não configura CORS. Em desenvolvimento o proxy do Vite
serve `/api`, e em produção o ASP.NET serve a SPA de `wwwroot`.
**Alternativas:** habilitar CORS com lista de origens permitidas.
**Motivo:** consequência direta da D-26. CORS só seria necessário se front e API
ficassem em origens diferentes — e é exatamente esse arranjo que a D-26 rejeita,
porque o cookie `SameSite=Lax` da D-20 não sobreviveria a ele. Adicionar CORS
"por precaução" mascararia o dia em que alguém quebrasse essa mesma origem: em
vez de falhar no navegador, o login passaria a falhar em silêncio.

## D-30 — Autenticação própria, e não o Supabase Auth
**Data:** 2026-09-07
**Decisão:** manter a autenticação própria da D-19, mesmo com o Supabase já em
uso para as fotos (D-26). O Supabase Auth fica descartado.
**Alternativas:** usar o Supabase Auth; mover o banco inteiro para o Supabase,
que tornaria a integração coerente.
**Motivo:** o Supabase Auth guarda os usuários no Postgres **dele**, e o banco da
aplicação é o do Render — e um banco não referencia tabela de outro. Como sete
chaves estrangeiras apontam para `users` (mecânico e criador da OS, autor do
item, autor da transição, quem subiu a mídia, quem enviou o orçamento, autor do
movimento de estoque), a tabela local seria necessária de qualquer forma, e
sobraria o trabalho de sincronizar os dois lados. Além disso, os papéis são
regra de negócio: quem pode fazer cada transição está no §5 do `data-model.md`,
não num provedor de identidade.

## D-31 — CI no GitHub Actions verificando cada PR
**Data:** 2026-09-07
**Decisão:** um workflow roda a cada pull request e a cada push na `main`:
testes do backend, lint e build do front, e build da imagem de deploy.
**Alternativas:** nenhuma verificação automática; verificar só o backend.
**Motivo:** o `README.md` libera merge sem revisão quando o review para por mais
de 12 horas — sem verificação automática, esse merge é às cegas. O build da
imagem entra porque é ela que o Render publica (D-26): um `Dockerfile` quebrado
precisa falhar no PR, não no deploy. O workflow dispensa subir um Postgres como
serviço porque, por D-24, os testes não dependem de banco.

## D-32 — A coluna "Aprovado" do Kanban é derivada, não um status
**Data:** 2026-09-07
**Decisão:** o quadro do protótipo tem uma coluna "Aprovado" entre "Aprovação do
Orçamento" e "Em Execução". Ela não vira status novo: mostra as OS em
`AWAITING_APPROVAL` que já têm orçamento aprovado. O `ServiceOrderSummary` ganha
`hasApprovedQuote` para o front distinguir as duas colunas.
**Alternativas:** criar o status `APPROVED` no enum; ou não ter a coluna.
**Motivo:** o status novo custaria migration, mudança na máquina de estados e
revisão dos 36 testes dela, a três semanas do fim — e a D-27 já mostrou o preço
de mexer no enum. A derivação entrega a mesma leitura para o atendente sem tocar
no modelo. **Consequência:** na Semana 2 a coluna fica sempre vazia, porque
orçamento só existe na Semana 3.

## D-33 — Sem prioridade de OS no MVP
**Data:** 2026-09-07
**Decisão:** o filtro "Todas Prioridades" do protótipo do Kanban não é
implementado, e `service_orders` não ganha coluna de prioridade.
**Alternativas:** acrescentar a coluna e o filtro.
**Motivo:** prioridade não aparece no schema, no `data-model.md` nem em nenhuma
decisão anterior — é escopo que entrou pelo desenho. Acrescentar campo de
domínio a três semanas do fim contraria a D-01, e o quadro é legível sem ele.
Fica para a Fase 2.

## D-34 — Sem tempo estimado de serviço no MVP
**Data:** 2026-09-08
**Decisão:** a tela do mecânico no protótipo mostra "Estimado: 45 min" e
"Falta: 15 min". Nem a duração prevista nem a contagem regressiva são
implementadas, e `service_orders` não ganha campo de tempo.
**Alternativas:** acrescentar `estimated_minutes` à OS ou ao item, com a
migration correspondente.
**Motivo:** o mesmo da D-33. Duração não aparece no schema nem em nenhuma
decisão anterior — entrou pelo desenho. Além do campo, exigiria decidir quem
estima, quando, e o que fazer quando o prazo estoura, o que é escopo de produto,
não de tela. A fila é utilizável sem isso. Fica para a Fase 2.
