# Publicação e banco de dados

Runbook de operação. Quem só quer rodar o projeto na máquina não precisa disto —
o `README.md` cobre esse caminho.

## Banco de dados

O schema é gerenciado **exclusivamente** por migrations do EF Core. Nenhuma
alteração é feita direto no banco.

```bash
cd backend
dotnet ef migrations add <NomeDaMigration>
dotnet ef database update
```

O DDL de referência fica em [`docs/schema.sql`](./schema.sql), para
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

A infraestrutura está descrita em [`render.yaml`](../render.yaml) e a imagem no
[`Dockerfile`](../Dockerfile). Para testar a imagem de produção localmente:

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
