# Sabemi TEC — Serviço de Webhooks de Pagamento

Serviço que recebe notificações de pagamento de um banco parceiro, garante que nenhuma seja
processada duas vezes, processa em background e exibe o resultado num painel administrativo.

## Como rodar

Pré-requisito: Docker Desktop.

```bash
docker compose up -d --build
```

Isso sobe três serviços — Postgres, migration (one-shot) e a API + dashboard (nginx):

- **Dashboard**: http://localhost:5173
- **API**: http://localhost:8080 (Swagger em `/swagger`)
- **ApiKeys de teste** (chaves separadas — veja "Autenticação"): webhook
  `dev-local-webhook-key`, dashboard `dev-local-dashboard-key` (header `X-Api-Key`)

Para gerar dados de exemplo, use a collection `requests/webhooks.http` (abre direto no
VS Code/Rider) ou:

```bash
curl -X POST http://localhost:8080/webhooks/payment \
  -H "Content-Type: application/json" -H "X-Api-Key: dev-local-webhook-key" \
  -d '{"id_transacao":"TX-001","id_contrato":"CT-42","valor":150.50,"data_pagamento":"2026-09-15T10:00:00Z","status":"PAGO"}'
```

O evento aparece no dashboard como **Pendente** e muda sozinho para **Sucesso** ~2 segundos
depois — processamento assíncrono em background.

### Carga sintética (opcional)

`SabemiTec.LoadSimulator` simula o banco parceiro: dispara lotes de transações concorrentes
contra `POST /webhooks/payment`, usando contratos reais buscados em `GET /webhooks/contracts`.

```bash
STG_WEBHOOK_API_KEY=<Webhook__ApiKey real do sabemi-api> \
  docker compose --profile simulator up -d --build
```

Por padrão aponta pra API de staging no Render; `LOAD_SIMULATOR_API_URL=http://api:8080` mira
na API local. `LOAD_SIMULATOR_INTERVAL_SECONDS` e `LOAD_SIMULATOR_CONCURRENCY` ajustam o
volume. `docker compose --profile simulator down` desliga.

## Stack

| | |
|---|---|
| Backend | .NET 8 · Minimal API · Dapper · Npgsql · DbUp |
| Banco | PostgreSQL 16 |
| Frontend | React 19 · Vite · TypeScript · TanStack Query · Tailwind CSS |
| Testes | xUnit · FluentAssertions · Testcontainers.PostgreSql |
| Ambiente | docker-compose (Postgres + migration + API + nginx) |

## Arquitetura

```
                         POST /webhooks/payment
                                    │
                    rate limit → ApiKey → ACL (traduz payload do banco)
                                    │
                        INSERT idempotente (ON CONFLICT)
                                    │
                         responde 202/200/400 (rápido)
                                    │
                       payment_event (processing_status = Pending)
                                    │
              worker faz polling (FOR UPDATE SKIP LOCKED) ──┐
                                                             ▼
                                          delay 2s (fora de transação)
                                                             │
                              transação: upsert contract_status + Processed
                                                             │
                                                    GET /api/payments
                                                             │
                                              dashboard (polling 5s)
```

Vertical slice por caso de uso (`Features/Webhooks`, `Features/Processing`,
`Features/Dashboard`) — cada requisito mora inteiro numa pasta, sem camadas técnicas globais.
Sem fila externa (Redis, RabbitMQ, SQS): `payment_event` é a própria fila (Inbox / Idempotent
Receiver), com colunas de controle de processamento junto do payload original.

## Padrões e boas práticas aplicadas

**Idempotência no banco.** Constraint única em `payment_event.transaction_id` +
`INSERT ... ON CONFLICT (transaction_id) DO NOTHING RETURNING id`. Zero linhas = duplicata
(`200`, `duplicate: true`); uma linha = novo (`202`). Sem `SELECT` de existência antes do
INSERT — só a constraint é atômica sob concorrência. Provado com 20 POSTs paralelos e mesmo
`id_transacao` → exatamente 1 linha (`WebhookIdempotencyTests`).

**Payload inválido é persistido, não descartado.** `valor <= 0`, data inválida ou status
desconhecido viram `is_valid = false` + `validation_error`, pra alimentar a aba "Eventos
inválidos" do dashboard. O endpoint lê o corpo como `JsonElement` (não faz binding tipado)
justamente pra não deixar o model binding rejeitar antes do handler gravar o evento.

**ACL isola o vocabulário do parceiro.** `ACL/PartnerBank/` é o único lugar que conhece
`id_transacao`, `id_contrato`, `"PAGO"`/`"FALHA"`. A tradução (`PartnerBankWebhookAcl`) nunca
lança exceção — devolve um resultado tipado ou a lista de erros. Suportar um segundo parceiro
é uma pasta nova implementando `IPaymentWebhookAcl`.

**Resposta rápida + processamento em background.** O `POST` faz um único INSERT e responde;
o delay de ~2s roda inteiramente no `PaymentEventProcessorWorker` (`BackgroundService` no
mesmo processo, sem `Task.Run` fire-and-forget). O worker reivindica lotes com
`FOR UPDATE SKIP LOCKED` (paralelismo seguro entre instâncias) e um lease por `locked_at`
(item de worker morto é reclamado depois de 2 min). O `Task.Delay` fica fora da transação de
negócio; upsert do contrato + marcar `Processed` ficam dentro da mesma transação. Falha de
negócio tenta de novo 3× (30s fixo), depois `DeadLettered`.

**`IUnitOfWork` injetado só onde há transação real.** Fora daí, os repositórios usam
`IDatabaseConnection` direto e deixam o Dapper abrir/fechar a conexão por chamada — nenhum
handler de leitura ou o `POST /webhooks/payment` (um único INSERT) precisa gerenciar conexão
manualmente.

| Caminho | Transação? |
|---|---|
| Worker: upsert do contrato + marcar `Processed` | Sim — únicas duas escritas atômicas juntas |
| `POST /webhooks/payment` | Não — um único INSERT |
| Claim do worker | Não — `UPDATE ... RETURNING` já é atômico |
| Leitura do dashboard | Não |

**Duas ApiKeys, mesmo header `X-Api-Key`.** `Webhook:ApiKey` protege `/webhooks/*` (só o
banco parceiro e o `LoadSimulator`); `Dashboard:ApiKey` protege `/api/payments*` (dashboard).
Chaves separadas porque a do dashboard sempre acaba do lado do cliente (bakeada no bundle JS
em produção) — um vazamento dela nunca deve abrir o webhook do parceiro.

**Autenticação do dashboard: proxy local, CORS em produção.** Localmente, nginx/Vite injetam
`X-Api-Key` via proxy — o browser nunca vê a chave. No Render (sem proxy cross-service
funcional), o browser chama a API diretamente com CORS liberado só pra `GET /api/payments*` e
a origem do `sabemi-web`, com a chave bakeada no bundle (`VITE_API_KEY` em
`.github/workflows/deploy-web.yml`).

**Rate limiting por perfil de tráfego.** Token bucket no webhook (absorve rajada legítima do
parceiro, `QueueLimit=0` — nunca segura a requisição esperando token). Fixed window no
dashboard (`120 req/min/IP` — tráfego de polling humano).

**Contratos como dado de referência.** Tabela `contract` (seed manual, migrations
`0002`-`0004`) enriquece o detalhamento do dashboard (tipo, parcelas, valor total) via
`LEFT JOIN` — não vem do payload do webhook. `GET /api/payments/contracts` (dashboard) e
`GET /webhooks/contracts` (worker, mesma chave do webhook) expõem a mesma lista.

**DI centralizado.** `Configurations/Extensions/ServicesExtensions.cs` concentra um método
`AddX` por área, encadeado fluentemente no `Program.cs`.

**Configuração por ambiente.** `appsettings.json` (defaults/prod não-secreta) →
`appsettings.Development.json` (valores reais de teste local, versionado) →
`appsettings.Production.json` (schema documentado, placeholders vazios) → variáveis de
ambiente (Render/docker-compose, sempre vencem).

**Frontend com polling.** TanStack Query, 5s, `keepPreviousData` pra tabela não piscar a cada
atualização. Cards de estatística são clicáveis e aplicam o filtro correspondente.

## Testes

```bash
dotnet test
```

21 testes (xUnit + FluentAssertions). Os de integração sobem Postgres real via
Testcontainers — sem mocks de banco, porque `ON CONFLICT`, `FOR UPDATE SKIP LOCKED` e o
upsert acumulativo não têm equivalente confiável em memória.

| Cenário | Onde |
|---|---|
| 20 POSTs paralelos, mesmo `id_transacao` → 1 linha | `WebhookIdempotencyTests` |
| Ciclo completo do worker (Pending → Processed, contrato atualizado) | `PaymentProcessingTests` |
| Múltiplos pagamentos no mesmo contrato, incluindo `FALHA` | `PaymentProcessingTests` |
| Filtro por status/contrato, paginação, stats | `DashboardQueryTests` |
| Rajada → 429 → reenvio ainda idempotente | `RateLimitingTests` |
| Validação de payload (campo a campo) | `PartnerBankWebhookAclTests` |
| Chave do webhook rejeitada no dashboard e vice-versa | `ApiKeyAuthTests` |

O worker fica desligado por padrão nos testes de endpoint (`workerEnabled: false` na
factory); os testes que o exercitam ligam explicitamente com `SimulatedDelayMs: 0`.

## Collection `.http`

`requests/webhooks.http` — abre direto em VS Code (extensão REST Client) ou Rider:

| # | Cenário | Resultado esperado |
|---|---|---|
| 1 | Pagamento válido | 202 Accepted |
| 2 | Mesmo `id_transacao` de novo | 200 OK, `duplicate: true` |
| 3 | ApiKey ausente | 401 |
| 4 | ApiKey inválida | 401 |
| 5 | Payload inválido (`valor` negativo) | 400, persistido como erro |
| 6 | Segundo pagamento no mesmo contrato | 202, soma em `contract_status` |
| 7 | JSON malformado | 400, não persiste |
| 8 | Sem `id_transacao` | 400, persistido com chave sintética |
| 9 | Rajada (250+ requisições rápidas) | 429 com `Retry-After` |

## Deploy (Render + Supabase, via GitHub Actions)

- `.github/workflows/ci.yml` — roda a suíte de testes a cada push em `main`.
- `.github/workflows/deploy-api.yml`, `deploy-web.yml` — manuais (`workflow_dispatch`), cada
  um autocontido: testa (só o da API), builda a imagem, publica no GHCR (`:latest`) e dispara
  o Deploy Hook do respectivo serviço no Render. Rodar o deploy de uma aplicação nunca builda
  a outra. O Render só puxa a imagem já publicada, não builda do repositório.

`SabemiTec.LoadSimulator` não tem workflow de deploy — roda local, apontando pra API de
staging.

### 1. Banco (Supabase)

1. Crie um projeto novo no Supabase.
2. Em **Project Settings → Database**, copie a **connection string direta** (porta `5432`,
   não a do pooler `6543` — DbUp e o Npgsql precisam de conexão direta pra
   `pg_advisory_lock`/prepared statements).
3. Monte a connection string no formato do Npgsql, com SSL:
   ```
   Host=db.<seu-projeto>.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=<sua-senha>;SSL Mode=Require;Trust Server Certificate=true
   ```
4. Aplique o schema manualmente contra essa connection string antes do primeiro deploy
   (`DatabaseMigrator` não roda mais automaticamente no startup).

### 2. Serviços (Render)

Crie **dois** Web Services, ambos como **"Existing Image"**:

**`sabemi-api`**
- Imagem: `ghcr.io/<seu-usuario>/<repo>-api:latest`
- Health check path: `/health`
- Variáveis de ambiente:
  | Nome | Valor |
  |---|---|
  | `ConnectionStrings__Default` | connection string do Supabase (passo 1) |
  | `Webhook__ApiKey` | chave real — só o banco parceiro deve ter essa |
  | `Dashboard__ApiKey` | outra chave real, diferente da de cima — vai parar num bundle JS público |
  | `Processing__SimulatedDelayMs` | `2000` (opcional, é o default) |
  | `Cors__DashboardOrigins__0` | URL pública do `sabemi-web` |
- `PORT` não precisa ser configurado (o Render injeta automaticamente).
- Copie a URL pública depois de criado — usada no `sabemi-web` e no secret abaixo.

**`sabemi-web`**
- Imagem: `ghcr.io/<seu-usuario>/<repo>-web:latest`
- Não precisa de variáveis de ambiente específicas.
- **Antes de dar push**: cadastre o secret `DASHBOARD_API_KEY` no GitHub (mesmo valor do
  `Dashboard__ApiKey` do `sabemi-api` — nunca o `Webhook__ApiKey`) — o CI passa ele como
  build-arg pro `npm run build`, bakeando no bundle JS.

Em ambos os serviços, pegue a **Deploy Hook URL** em *Settings → Deploy Hook*.

**Visibilidade do pacote no GHCR**: torne as duas imagens públicas (*Package settings →
Change visibility*) ou configure uma credencial de registry no Render com um PAT
`read:packages`.

### 3. Secrets no GitHub

Em *Settings → Secrets and variables → Actions*:

| Secret | Valor |
|---|---|
| `RENDER_DEPLOY_HOOK_API` | Deploy Hook do serviço `sabemi-api` |
| `RENDER_DEPLOY_HOOK_WEB` | Deploy Hook do serviço `sabemi-web` |
| `DASHBOARD_API_KEY` | mesma chave de `Dashboard__ApiKey` no `sabemi-api` — nunca o `Webhook__ApiKey` |

`GITHUB_TOKEN` já existe automaticamente, não precisa criar.

## Estrutura

```
src/SabemiTec.Api/
├── ACL/PartnerBank/        # fronteira com o payload do banco parceiro
├── Enum/                   # enums-como-classe (Enumeration + derivados)
├── Features/
│   ├── Webhooks/           # POST /webhooks/payment, GET /webhooks/contracts
│   ├── Processing/         # worker: claim, delay, upsert, retry/dead-letter
│   └── Dashboard/          # GET /api/payments(/stats|/contracts|/{id}|/invalid)
├── Database/PostgreSQL/    # IUnitOfWork, Dapper, SQL, migrations (DbUp)
│   └── Migrations/Scripts/ # 0001 schema · 0002-0004 seed/ajustes de contract (demo)
├── Middlewares/            # ApiKeyAuthMiddleware
└── Configurations/         # DI, RateLimiting, mapeamento de rotas

src/SabemiTec.LoadSimulator/  # worker standalone: gera carga sintética em POST /webhooks/payment

web/src/
├── api/                    # client, chamadas, tipos
├── hooks/                  # TanStack Query + filtros na URL
├── components/             # tabela, filtros, badges, stats
└── pages/Dashboard.tsx

tests/SabemiTec.Tests/
├── Unit/                   # ACL, validação
└── Integration/            # Testcontainers + WebApplicationFactory
```
