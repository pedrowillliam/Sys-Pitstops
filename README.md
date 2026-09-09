# Sys Pitstops

ERP em Progressive Web App para gestão de oficinas mecânicas independentes.
Projeto da disciplina **Projeto de Desenvolvimento de Software (2026.1)** —
Bacharelado em Ciência da Computação, UFAPE.

**Equipe:** Fábio Medeiros, João Santos e Pedro William
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

- .NET SDK 10
- Node.js 20+
- Docker e Docker Compose

A CLI do EF Core é instalada uma vez por máquina:

```bash
dotnet tool install --global dotnet-ef
```

## Como rodar

```bash
# 1. Clonar e configurar variáveis de ambiente
git clone https://github.com/pedrowillliam/Sys-Pitstops.git
cd Sys-Pitstops
cp .env.example .env

# 2. Subir o banco
docker compose up -d

# 3. Back-end (http://localhost:5000, Swagger em /swagger)
cd backend
dotnet restore
dotnet ef database update --project src/SysPitstops.Api
dotnet run --project src/SysPitstops.Api

# 4. Front-end (http://localhost:5173)
cd ../frontend
npm install
npm run dev
```

`backend/` contém a solution com dois projetos, por isso o `--project`: sem ele
o `dotnet run` não sabe qual executar.

Se a porta 5432 já estiver ocupada — uma instalação nativa do PostgreSQL na
máquina, por exemplo — mude `POSTGRES_PORT` no `.env` e ajuste a porta na
`ConnectionStrings__Default` junto. O `docker-compose.yml` já lê a variável.

Em desenvolvimento o front roda no Vite e faz proxy de `/api` para o back —
front e API na mesma origem, que é o que o cookie de sessão exige (D-20).

### Cliente TypeScript da API

Os tipos de requisição e resposta são **gerados** do Swagger, nunca escritos à
mão (D-17, D-18). Depois de mexer em qualquer rota ou DTO, com o back rodando:

```bash
cd frontend
npm run generate:api   # reescreve src/api/schema.d.ts
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

Em produção a migration é aplicada **na subida da aplicação** (D-28): não há
passo manual no deploy.

## Publicação

O sistema está no ar em **https://syspitstops.onrender.com**.

O plano gratuito hiberna o serviço após alguns minutos sem tráfego, então o
primeiro acesso depois de uma pausa demora. Antes de uma apresentação, abra o
sistema com antecedência para acordá-lo.

Um **único serviço** no Render: o ASP.NET serve a API e a SPA já compilada, mais
o Postgres gerenciado (D-26). Dois serviços separados colocariam front e API em
origens diferentes e o cookie `SameSite=Lax` deixaria de ser enviado — o login
passaria em desenvolvimento e falharia em produção.

A infraestrutura está descrita em [`render.yaml`](./render.yaml) e a imagem no
[`Dockerfile`](./Dockerfile). Para testar a imagem de produção localmente:

```bash
docker build -t syspitstops:local .
docker run --rm -p 8080:10000 -e PORT=10000 \
  -e "ConnectionStrings__Default=Host=host.docker.internal;Port=5432;Database=syspitstops;Username=syspitstops;Password=changeme" \
  -e JWT__Issuer=syspitstops -e JWT__Audience=syspitstops \
  -e "JWT__Secret=um-segredo-com-pelo-menos-32-bytes-aqui" \
  -e SEED_ADMIN_EMAIL=admin@syspitstops.local -e SEED_ADMIN_PASSWORD=changeme \
  syspitstops:local
# http://localhost:8080
```

### Criar o serviço no Render

Já foi feito uma vez. O passo a passo fica registrado porque o Postgres gratuito
expira, e recriá-lo exige refazer os passos 2 e 3.

1. **New → Blueprint**, apontando para este repositório. O Render lê o
   `render.yaml` e propõe criar o serviço web e o banco.
2. Os campos marcados como `sync: false` **não** vêm do arquivo e precisam ser
   preenchidos no painel — é onde ficam os segredos:

   | Variável | O que pôr |
   |---|---|
   | `ConnectionStrings__Default` | a string do banco, no formato do Npgsql (abaixo) |
   | `SEED_ADMIN_EMAIL` | e-mail do primeiro admin |
   | `SEED_ADMIN_PASSWORD` | senha do primeiro admin, para trocar no primeiro acesso |

   `JWT__Secret` é gerado pelo próprio Render (`generateValue`) e não aparece em
   lugar nenhum do repositório.

3. **Converta a string do banco.** O Render entrega a *Internal Database URL* no
   formato `postgres://usuario:senha@host:5432/banco`, que o Npgsql não entende.
   O formato esperado é:

   ```
   Host=<host>;Port=5432;Database=<banco>;Username=<usuario>;Password=<senha>;SSL Mode=Require;Trust Server Certificate=true
   ```

   Use a URL **interna**, não a externa: ela não sai da rede do Render.

4. O primeiro deploy cria o schema sozinho (D-28) e semeia o admin (D-22). O
   Render só considera o serviço no ar quando `GET /health` responde 200.

**Plano gratuito, dois limites que mordem:** o serviço hiberna após 15 minutos
sem tráfego e a primeira requisição seguinte demora alguns segundos; e o
Postgres gratuito **expira em 30 dias**, quando é preciso criar outro e refazer
o passo 2. Para a banca do projeto, vale conferir isso na véspera.

## Convenções

**Idioma.** Código, banco de dados e nomes de rotas em **inglês**. Interface,
documentação, corpo de PR e mensagens de commit em **português**.

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

**Commits.** Conventional Commits, com o texto em **português** e no imperativo.
O tipo (`feat`, `fix`, `docs`, `chore`, `ci`) permanece em inglês.

```
feat(service-orders): adiciona a máquina de estados da OS com testes
fix(api): usa KnownIPNetworks no lugar da API obsoleta
docs: registra ordem alfabética dos enums (D-27)
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
