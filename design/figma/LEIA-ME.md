# Referência de telas — Figma "Sys-PitStop"

**Material de consulta, não código de produção.** Nada aqui é compilado nem
entra no build: são as telas do protótipo, versionadas para que a equipe
inteira trabalhe a partir da mesma referência sem gastar chamada do conector.

Arquivo: `jGZ6p2AhbpQAVcy7wV6K9o` · extraído em 2026-09-03 pelo conector do Figma.

## O que tem aqui

| Pasta / arquivo | Conteúdo |
|---|---|
| `img/` | PNG de cada tela, no tamanho original do frame |
| `html/*.tsx` | Código de referência do conector (React + Tailwind) |
| `html/*.html` | O mesmo, convertido para HTML autônomo — abre no navegador |
| `assets/` | Imagens usadas pelas telas (logo), já baixadas |
| `estrutura-completa.xml` | **Estrutura das 32 telas**: nome, posição, tamanho e todos os textos de cada camada |
| `tsx-para-html.py` | Conversor `.tsx` → `.html` |

Para ver as telas HTML:

```bash
cd design/figma
python -m http.server 8899
# abre http://127.0.0.1:8899/html/
```

## Cobertura da extração

O plano do Figma é **Starter: 20 chamadas de MCP por mês**, e elas acabaram
nesta extração. Por isso a cobertura é parcial:

**Com imagem (15):** login-administrador, login-atendente, kanban-atendente,
kanban-administrador, dashboard-administrador, mecanicos-atendente,
mecanicos-administrador, cliente-atendente, cliente-administrador,
orcamento-atendente, orcamento-administrador, estoque-atendente,
estoque-administrador, cotacao-atendente, cotacao-administrador.

**Com código (1):** login-administrador.

**Sem nada além da estrutura no XML (17):** as 8 telas de cadastro (cliente,
serviço, estoque, funcionário — em duas versões), as 5 telas mobile (login e
4 do mecânico), 2 de carregamento e 2 de "sucesso login".

O `estrutura-completa.xml` cobre **as 32**, com todos os rótulos de texto e a
geometria de cada camada — dá para reconstruir uma tela a partir dele somado à
imagem, sem gastar chamada.

### Para extrair o resto

O limite renova mensalmente; um plano Professional/Education sobe para 200
chamadas por dia. Com quota disponível, por tela são 2 chamadas:

```
get_screenshot(fileKey, nodeId, maxDimension=1728)   -> baixar o PNG em img/
get_design_context(fileKey, nodeId)                  -> salvar em html/<slug>.tsx
python tsx-para-html.py "html/*.tsx"                 -> gera o .html
```

Node ids que faltam:

| Tela | node-id |
|---|---|
| Carregamento (1) | `84:578` |
| Carregamento (2) | `84:588` |
| Cadastro de Cliente (1) | `80:1314` |
| Cadastro serviço (1) | `80:1315` |
| Cadastro Estoque (1) | `80:1316` |
| Cadastro Funcionário (1) | `80:1317` |
| Cadastro de Cliente (2) | `84:639` |
| Cadastro serviço (2) | `84:780` |
| Cadastro Estoque (2) | `84:913` |
| Cadastro Funcionário (2) | `84:1033` |
| Mobile — login | `80:1320` |
| Mobile — Mecânico (1) | `80:1323` |
| Mobile — Mecânico (2) | `92:1249` |
| Mobile — Mecânico (3) | `92:1306` |
| Mobile — Mecânico (4) | `80:1324` |
| Sucesso login 2 | `84:154` |
| Sucesso login 3 | `89:1186` |

E os que já foram, caso precise do código: `80:1295` login-administrador,
`80:1298` login-atendente, `80:1299` kanban-atendente, `80:1300`
kanban-administrador, `80:1301` dashboard-administrador, `80:1302`
mecanicos-atendente, `80:1304` mecanicos-administrador, `80:1305`
cliente-atendente, `80:1306` cliente-administrador, `80:1308`
orcamento-atendente, `80:1309` estoque-atendente, `80:1310`
orcamento-administrador, `80:1311` estoque-administrador, `80:1312`
cotacao-atendente, `80:1313` cotacao-administrador.

## Sobre o código de referência

O conector devolve React + Tailwind com **tudo posicionado em absoluto** a
partir das coordenadas do Figma. Não é código para copiar: serve para ler
cores, tamanhos, espaçamentos e textos exatos. A implementação em
`frontend/` segue a stack e as convenções do projeto.

O conversor conserta três coisas do que o Figma devolve:

1. `<div … />` — auto-fechamento não existe em HTML para `div`;
2. `absolute contents` — o `absolute` não tem efeito com `display: contents` e
   atrapalha se o `contents` não valer;
3. `font-['Montserrat:SemiBold']` — não é uma família válida; o peso vem em
   classe separada.

## Divergências entre o protótipo e o modelo de dados

Anotado ao ler as telas. A maior parte já virou decisão registrada em
`docs/decisions.md` — o que continua em aberto está marcado.

- **O Kanban tem 7 colunas:** Marcado, No Pátio, Em Análise, Aprovação do
  Orçamentos, Aprovado, Em Execução, Pronto. "Aprovado" não corresponde a
  status nenhum: pela D-13 a aprovação do orçamento já leva
  `AWAITING_APPROVAL → IN_PROGRESS` automaticamente.
  **Resolvido pela D-35** — a coluna sai e o fato vira etiqueta no cartão,
  visível em qualquer coluna onde a OS estiver.
- **Não há coluna `DELIVERED` no quadro**, embora o status exista. O quadro
  implementado em `frontend/src/service-orders/board.ts` para em "Pronto",
  como o protótipo.
- **O menu lateral muda por papel:** administrador com Dashboard no topo,
  atendente com a mesma lista sem ele. **Implementado** em
  `frontend/src/layout/navigation.ts`.
- **Não existe "Veículos" como seção própria.** O veículo aparece dentro da OS
  e da busca do topo ("Buscar por placa ou cliente…"), nunca como lista
  separada. A rota `/vehicles` existe porque a API existe, mas fica fora do
  menu de propósito.
- **"Mecânicos" é uma seção de gestão de pessoas**, que a D-01 não lista no
  escopo do MVP e para a qual não há tabela além de `users`. **Em aberto** — a
  tela segue placeholder e precisa de decisão antes de virar código.
- **O mecânico não tem tela de desktop.** As quatro telas dele são mobile
  (440×956), o que combina com trabalhar no pátio pelo celular.
  **Implementado** como `/my-queue`, para onde ele cai ao entrar.
- **A tela do mecânico mostra tempo estimado e contagem regressiva.** Não há
  campo de duração em `service_orders`. **Cortado pela D-34**, fica para a
  Fase 2.
- **A abertura da OS traz campos sem coluna** — "Serviços a Realizar", "Valor
  Estimado (R$)" e as duas caixas de texto livre. **Cortados pela D-37**:
  serviço é item com preço congelado (D-07) e o total é calculado, nunca
  armazenado (D-09).
