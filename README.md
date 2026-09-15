# Sys Pitstops

ERP em Progressive Web App para gestão de oficinas mecânicas independentes.
Projeto da disciplina **Projeto de Desenvolvimento de Software (2026.1)** —
Bacharelado em Ciência da Computação, UFAPE.

**Equipe:** Fábio Medeiros, João Santos e Pedro William
**Orientação:** Prof. Rodrigo Gusmão De Carvalho Rocha

O sistema está no ar em **https://syspitstops.onrender.com**. O plano gratuito
hiberna o serviço após alguns minutos sem tráfego, então o primeiro acesso
depois de uma pausa demora — abra com antecedência antes de uma apresentação.

---

## Sobre

O sistema cobre o fluxo completo de atendimento de uma oficina — da entrada do
veículo à entrega — substituindo o controle em caderno e a negociação informal
por registro rastreável: ordem de serviço, histórico técnico por veículo,
orçamento com aprovação registrada e acompanhamento pelo cliente.

## Stack

| Camada | Tecnologia |
|---|---|
| Back-end | C# / ASP.NET Core (Web API) + Entity Framework Core |
| Front-end | React + Vite + TypeScript (PWA) |
| Banco | PostgreSQL |
| Estilo | Tailwind CSS |
| Dados no cliente | TanStack Query |

```
/backend    API, domínio e migrations
/frontend   PWA
/docs       Modelo de dados, schema, decisões e publicação
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
front e API na mesma origem, que é o que o cookie de sessão exige.

### Primeiro acesso

Na primeira subida a aplicação cria a oficina e um administrador, com o e-mail e
a senha que estiverem no `.env`. Copiando o `.env.example` sem alterar, são:

| | |
|---|---|
| E-mail | `admin@syspitstops.local` |
| Senha | `changeme` |

Troque a senha em **Configurações** depois de entrar. Em produção esses dois
valores são preenchidos no painel, nunca no arquivo.

## Carga de demonstração

O sistema sobe vazio: são criados apenas a oficina e o usuário administrador.
Um quadro sem nenhum cartão se parece com um sistema quebrado, então para
desenvolver ou apresentar vale ligar a carga:

```bash
cd backend
SEED_DEMO=true SEED_DEMO_PASSWORD=demo1234 dotnet run --project src/SysPitstops.Api
```

Ela cria seis meses de oficina fictícia — clientes com veículos, mecânicos,
peças com movimentação e ordens de serviço espalhadas pelos meses, com itens,
histórico de transições, orçamentos e a baixa de estoque das que foram
concluídas.

**Roda uma vez só.** Se o banco já tiver algum cliente, é ignorada.

Os usuários criados entram com `SEED_DEMO_PASSWORD` e e-mail derivado do nome
(`roberto.silva@syspitstops.local`, `ana.lima@syspitstops.local`). Sem a
variável, eles existem para serem atribuídos às OS, mas ninguém entra como eles.

Para recomeçar do zero:

```bash
docker exec syspitstops-db psql -U syspitstops -d syspitstops \
  -c "DROP SCHEMA public CASCADE; CREATE SCHEMA public;"
```

## Documentação

O modelo de dados e o registro de decisões técnicas estão em
[`docs/`](./docs). Para publicar ou recriar o serviço, veja
[`docs/deploy.md`](./docs/deploy.md).
