# Sys Pitstops

ERP em Progressive Web App para gestão de oficinas mecânicas independentes.
Projeto da disciplina **Projeto de Desenvolvimento de Software (2026.1)** —
Bacharelado em Ciência da Computação, UFAPE.

**Equipe:** Fábio Medeiros, João Santos e Pedro da Silva
**Orientação:** Prof. Rodrigo Gusmão De Carvalho Rocha

---

## Sobre

O sistema cobre o fluxo completo de atendimento de uma oficina — da entrada do
veículo à entrega — substituindo o controle em caderno e a negociação informal
por registro rastreável: ordem de serviço, histórico técnico por veículo,
orçamento com aprovação registrada e acompanhamento pelo cliente.

Escopo do MVP, roadmap e justificativa das decisões estão em
[`docs/`](./docs).

## Stack

| Camada | Tecnologia |
|---|---|
| Back-end | C# / ASP.NET Core (Web API) + Entity Framework Core |
| Front-end | React + Vite + TypeScript (PWA) |
| Banco | PostgreSQL |
| Estilo | Tailwind CSS |
| Dados no cliente | TanStack Query |

## Estrutura

```
/backend    API, domínio e migrations
/frontend   PWA
/docs       Modelo de dados, schema e decisões
```

## Pré-requisitos

- .NET SDK 8+
- Node.js 20+
- Docker e Docker Compose

## Como rodar

```bash
# 1. Clonar e configurar variáveis de ambiente
git clone git@github.com:<org>/sys-pitstops.git
cd sys-pitstops
cp .env.example .env

# 2. Subir o banco
docker compose up -d

# 3. Back-end (http://localhost:5000, Swagger em /swagger)
cd backend
dotnet restore
dotnet ef database update
dotnet run

# 4. Front-end (http://localhost:5173)
cd ../frontend
npm install
npm run dev
```

## Banco de dados

O schema é gerenciado **exclusivamente** por migrations do EF Core. Nenhuma
alteração é feita direto no banco.

```bash
cd backend
dotnet ef migrations add <NomeDaMigration>
dotnet ef database update
```

O DDL de referência fica em [`docs/schema.sql`](./docs/schema.sql), para
consulta — ele não é aplicado manualmente.

## Convenções

**Idioma.** Código, banco de dados, nomes de rotas e commits em **inglês**.
Interface e documentação em **português**.

**Tipos.** Dinheiro em `numeric(12,2)` no banco e `decimal` no C# — nunca
`float` ou `double`. Datas em `timestamptz`, sempre em UTC; conversão de fuso
só na exibição.

**Branches.**

```
main                      protegida, sempre publicável
feat/<escopo>             nova funcionalidade
fix/<escopo>              correção
docs/<escopo>             documentação
```

**Commits.** Conventional Commits, em inglês e no imperativo.

```
feat(service-order): add status transition endpoint
fix(stock): prevent negative quantity on write-off
docs(readme): add setup instructions
```

**Pull requests.** Toda mudança entra por PR com pelo menos uma aprovação.
Revisão parada por mais de 12 horas libera merge direto — o prazo do projeto
não comporta review como gargalo. O PR deve referenciar a issue correspondente.

## Documentação

| Arquivo | Conteúdo |
|---|---|
| [`docs/data-model.md`](./docs/data-model.md) | Modelo de dados, regras de negócio e decisões de modelagem |
| [`docs/schema.sql`](./docs/schema.sql) | DDL de referência do PostgreSQL |
| [`docs/decisions.md`](./docs/decisions.md) | Registro de decisões técnicas |

O contrato da API é gerado automaticamente pelo Swagger em
`http://localhost:5000/swagger`. O cliente TypeScript do front é gerado a
partir dele — tipos de requisição e resposta não são escritos à mão.
