# Sabemi TEC — Serviço de Webhooks de Pagamento

Avaliação de habilidades técnicas: um serviço que recebe notificações de pagamento de um
banco parceiro, garante que nenhuma seja processada duas vezes, processa em background e
exibe o resultado num painel administrativo.

## Como rodar

Pré-requisito: Docker Desktop.

```bash
docker compose up -d --build
```

Isso sobe três serviços — Postgres, API e o dashboard (servido por nginx) — aplica as
migrations automaticamente e deixa tudo pronto:

- **Dashboard**: http://localhost:5173
- **API**: http://localhost:8080 (Swagger em `/swagger`)
- **ApiKey de teste**: `dev-local-key` (header `X-Api-Key`, configurável em `.env` a partir
  de `.env.example`)

Para gerar dados de exemplo, use a collection `requests/webhooks.http` (abre direto no
VS Code/Rider) ou:

```bash
curl -X POST http://localhost:8080/webhooks/pagamento \
  -H "Content-Type: application/json" -H "X-Api-Key: dev-local-key" \
  -d '{"id_transacao":"TX-001","id_contrato":"CT-42","valor":150.50,"data_pagamento":"2026-09-15T10:00:00Z","status":"PAGO"}'
```

O evento aparece no dashboard como **Pendente** e muda sozinho para **Sucesso** ~2 segundos
depois — é o processamento assíncrono em ação.

## Stack

| | |
|---|---|
| Backend | .NET 8 · Minimal API · Dapper · Npgsql · DbUp |
| Banco | PostgreSQL 16 |
| Frontend | React 19 · Vite · TypeScript · TanStack Query · Tailwind CSS |
| Testes | xUnit · FluentAssertions · Testcontainers.PostgreSql |
| Ambiente | docker-compose (Postgres + API + nginx) |

## Arquitetura

```
                         POST /webhooks/pagamento
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

## Decisões e trade-offs

Esta seção existe porque, num teste técnico com requisitos de idempotência e resiliência
explícitos no enunciado, a *justificativa* das escolhas pesa tanto quanto o código.

### Idempotência mora no banco, não na aplicação

A garantia é uma constraint única em `payment_event.transaction_id`, e o INSERT usa
`ON CONFLICT (transaction_id) DO NOTHING RETURNING id`. Zero linhas retornadas = duplicata;
uma linha = inserido agora. **Não existe um `SELECT` de existência antes do INSERT** — essa é
a armadilha clássica: duas requisições concorrentes (o cenário real de "o banco reenviou por
timeout") passam pelo `SELECT` sem se ver e ambas inserem. Só a constraint é atômica sob
concorrência. Provado com um teste que dispara 20 POSTs **paralelos** com o mesmo
`id_transacao` e verifica exatamente 1 linha, 1×202 e 19×200 (`WebhookIdempotencyTests`).

**Duplicata responde 200, não 409.** Um cliente de webhook trata qualquer não-2xx como falha
de entrega e reenvia — 409 na duplicata criaria um loop de reenvio eterno por algo que, do
ponto de vista do parceiro, já deu certo. 202 é reservado para quando algo novo foi de fato
aceito para processamento; a duplicata recebe 200 com `duplicate: true` no corpo.

### O "Log de Eventos Brutos" é a própria fila

Em vez de uma tabela de eventos e uma tabela de fila separadas, `payment_event` é as duas
coisas: guarda o `payload` original (jsonb, íntegro) e as colunas de controle de
processamento (`processing_status`, `attempts`, `available_at`, `locked_at`). Com tabelas
separadas, gravar o evento e enfileirá-lo seriam dois INSERTs exigindo uma transação cujo
único propósito seria compensar uma separação artificial. Fundidos, registrar o evento **é**
enfileirá-lo.

Tecnicamente isto é **Inbox / Idempotent Receiver**, não Outbox — Outbox resolve "publicar
mensagem de dentro de uma transação de negócio" (saída); aqui o problema é "receber uma
mensagem possivelmente duplicada e processar exatamente uma vez" (entrada).

Payload que falha na validação (`valor <= 0`, data inválida, status desconhecido) **também é
persistido**, marcado com `is_valid = false` e `validation_error` preenchido — é assim que o
requisito de "visualização de erros" do dashboard é atendido. Por isso o endpoint lê o corpo
como `JsonElement`, não faz binding tipado direto: um binding tipado seria rejeitado pelo
model binding do ASP.NET *antes* do handler rodar, e o evento nunca chegaria a ser gravado.

### Resiliência: resposta rápida + processamento em background

O `POST` faz **um único INSERT** e responde (não há nada mais rápido possível). O delay de 2s
que simula o processamento pesado vive inteiramente no `PaymentEventProcessorWorker`, um
`BackgroundService` dentro do mesmo processo da API — sem fila externa (Redis, RabbitMQ),
sem serviço separado. A durabilidade vem da linha já commitada em `payment_event`, não de
memória: **não existe `Task.Run` fire-and-forget** em lugar nenhum, porque essa é a solução
ingênua que perde a notificação de pagamento se o processo reiniciar no meio.

O worker reivindica lotes com:

```sql
update payment_event e set processing_status = 1, attempts = attempts + 1, locked_at = now()
from ( select id from payment_event
        where (processing_status = 0 and available_at <= now())
           or (processing_status = 1 and locked_at < now() - @LeaseSeconds * interval '1 second')
        for update skip locked limit @BatchSize ) as c
where e.id = c.id returning ...;
```

`FOR UPDATE SKIP LOCKED` é o que permite múltiplas instâncias da API processarem em paralelo
sem pegar o mesmo item duas vezes. A cláusula do lease (`locked_at` vencido) é o que faz o
sistema **sobreviver a um crash**: se o worker morre no meio, o item fica preso em
`processing_status = 1`, mas depois de 2 minutos qualquer worker o reclama de novo — testado
na prática derrubando o container da API no meio do processamento.

O `Task.Delay(2s)` fica **fora** da transação de negócio (upsert do contrato + marcar
`Processed`), porque segurá-la aberta por 2s prenderia conexão do pool e locks de linha do
contrato sob lote paralelo. As duas escritas de negócio, por sua vez, ficam **dentro** da
mesma transação — se o processo morresse entre elas sem isso, um crash faria o pagamento ser
contado duas vezes no reprocessamento ou desaparecer. `MarkProcessedAsync` só afeta a linha se
`processing_status = 1` ainda; se outro worker já reclamou o item (lease expirado), a
atualização do contrato é desfeita via rollback em vez de aplicada duas vezes.

Falha de negócio (não crash) tenta de novo 3 vezes com espera fixa de 30s, depois vira
`DeadLettered` — sem backoff exponencial/jitter, porque com um único parceiro e volume de
teste isso seria complexidade sem gargalo real para justificar.

### Dapper + Unit of Work — onde o padrão se paga e onde não

Sem ORM: as queries de idempotência (`ON CONFLICT`), claim (`SKIP LOCKED`) e o upsert
acumulativo do contrato são a regra de negócio, e escondê-las atrás de um ORM pioraria a
legibilidade exatamente do que mais importa aqui.

O `IUnitOfWork` é usado **apenas** onde existe uma transação real:

| Caminho | Usa transação? | Por quê |
|---|---|---|
| Worker: upsert do contrato + marcar `Processed` | **Sim** | Duas escritas que precisam ser atômicas — a única transação real do sistema |
| `POST /webhooks/pagamento` | Não | Um único INSERT; abrir transação seria cerimônia |
| Claim do worker | Não | `UPDATE ... RETURNING` já é atômico em autocommit |
| Leitura do dashboard | Não | Transação para um `SELECT` é ruído |

Aplicar Unit of Work em tudo por simetria seria o mesmo tipo de over-engineering que se evitou
em outros pontos do projeto.

### ACL — fronteira com o vocabulário do banco parceiro

`ACL/PartnerBank/` isola o dialeto do parceiro (`id_transacao`, `id_contrato`, `"PAGO"` /
`"FALHA"`, snake_case) do resto do sistema. O DTO (`PaymentWebhookPayload`) é o único lugar do
código que conhece os nomes de campo originais; a tradução (`PartnerBankWebhookAcl`) devolve
um resultado tipado ou a lista de erros de validação — nunca lança exceção, porque o payload
inválido precisa ser persistido, não descartado. Trocar de banco parceiro, ou suportar um
segundo, é uma pasta nova implementando `IPaymentWebhookAcl`, sem tocar worker nem dashboard.

### Rate limiting: token bucket, só no `/webhooks`

Notificação bancária chega em rajada (o parceiro fecha um lote e dispara tudo de uma vez),
não em fluxo constante — por isso token bucket, e não fixed/sliding window, que penalizariam
a rajada legítima. `QueueLimit = 0` é deliberado: enfileirar significa segurar a requisição do
banco esperando token, contradizendo o requisito de responder rápido — melhor 429 imediato do
que segurar por segundos. A política existe só no grupo `/webhooks`; o dashboard não tem rate
limit porque o polling de um browser não é o tráfego que precisa de proteção aqui.

O ponto que mais importa: **o 429 acontece antes de qualquer persistência**, então o reenvio
que ele provoca cai no mesmo caminho idempotente de sempre — nunca há "throttle por
idempotência" nem duplicata contabilizada por causa de um 429. Provado em
`RateLimitingTests`: uma rajada estoura o limite, e reenviar a transação rejeitada resulta em
exatamente uma linha no banco.

### Vertical slice, não Clean Architecture / DDD tático / hexagonal

`Features/<Webhooks|Processing|Dashboard>/` agrupa por caso de uso, não por camada técnica —
cada requisito do PDF mora inteiro numa pasta. Descartado deliberadamente:

- **Camadas por assembly** (`Api`/`Application`/`Infrastructure`) dariam uma implementação
  por abstração num serviço com um endpoint de escrita e dois de leitura.
- **DDD tático** (agregados, value objects) não se aplica: a única invariante de negócio
  (soma do contrato) é correta porque vive no `ON CONFLICT DO UPDATE` do SQL, não porque um
  agregado a protege — modelar isso como agregado rico reintroduziria o problema de
  concorrência que o SQL já resolve.
- **Hexagonal** paga quando há múltiplos adaptadores de cada lado. Aqui há um driving (HTTP)
  e um driven (Postgres); a ACL já cumpre o papel de porta onde de fato existe um adaptador
  plausível (um segundo banco parceiro).

Convenção de DI: `Configurations/Extensions/ServicesExtensions.cs` concentra métodos `AddX`
por área, encadeados fluentemente no `Program.cs` — nenhum `services.AddScoped<>()` solto.

### Dois status, uma ambiguidade resolvida

O enunciado pede filtro "Sucesso/Erro", mas existem dois conceitos de status: o que o banco
informou (`payment_status`: PAGO/FALHA) e o estado do nosso processamento
(`processing_status`: Pending/Processing/Processed/Failed/DeadLettered). Resolvido com um
campo derivado, `effectiveStatus`, calculado em SQL:

| Condição | `effectiveStatus` |
|---|---|
| payload inválido ou dead-lettered | `Error` |
| ainda pendente/processando | `Pending` |
| processado e `payment_status = PAGO` | `Success` |
| processado mas `payment_status ≠ PAGO` | `Error` |

### Frontend: polling, não SSE/WebSocket

O enunciado aceita "tempo real **ou** via refresh". Polling de 5s (TanStack Query,
`keepPreviousData` para a tabela não piscar a cada atualização) entrega o mesmo valor
percebido por uma fração do custo de SSE — que exigiria endpoint dedicado, gestão de
reconexão e uma segunda forma de invalidar cache. Cada card de estatística também é
clicável e aplica o filtro correspondente.

### O que ficou fora de escopo, e por quê

| Item | Motivo |
|---|---|
| Observabilidade avançada (Serilog, health checks elaborados) | `/health` simples já cobre o que o compose precisa; o resto é peso sem uso num teste técnico |
| HMAC/`X-Signature` | O enunciado pede "validação simples" — ApiKey em tempo constante atende; a estrutura do filtro comporta a evolução |
| Rate limit distribuído (Redis) | O limiter nativo é em memória, por instância — correto para uma única réplica, documentado como limitação |
| Autenticação de usuário no dashboard | Reutiliza a mesma ApiKey; em produção seria OIDC + BFF, já que uma chave em bundle JS não é secreta |
| Worker como serviço .NET separado | Ganho de isolamento não justifica dois deploys/Dockerfiles para o volume deste teste |
| Backoff exponencial com jitter | Retry fixo (3× / 30s) é suficiente sem múltiplos parceiros de alto volume |

## Testes

```bash
dotnet test
```

19 testes (xUnit + FluentAssertions), zero mocks de banco — os de integração sobem um
Postgres real via Testcontainers, porque as garantias centrais (`ON CONFLICT`,
`FOR UPDATE SKIP LOCKED`, upsert acumulativo) não têm equivalente em memória; testá-las
contra um dublê seria testar o dublê.

| Cenário | Onde |
|---|---|
| 20 POSTs paralelos, mesmo `id_transacao` → 1 linha | `WebhookIdempotencyTests` |
| Ciclo completo do worker (Pending → Processed, contrato atualizado) | `PaymentProcessingTests` |
| Múltiplos pagamentos no mesmo contrato, incluindo `FALHA` | `PaymentProcessingTests` |
| Filtro por status/contrato, paginação, stats | `DashboardQueryTests` |
| Rajada → 429 → reenvio ainda idempotente | `RateLimitingTests` |
| Validação de payload (campo a campo) | `PartnerBankWebhookAclTests` |

O worker fica desligado por padrão nos testes de endpoint (`workerEnabled: false` na
factory) para não competir com as asserções; os testes que o exercitam ligam explicitamente
com `SimulatedDelayMs: 0`.

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

Pipeline: push em `main` → `.github/workflows/deploy.yml` roda a suíte de testes → builda e
publica as duas imagens Docker no GitHub Container Registry (`ghcr.io`) → dispara o redeploy
no Render via Deploy Hook. O Render **não** builda a partir do repositório — ele só puxa a
imagem já pronta, então a etapa de build/teste do CI é o gate real antes de qualquer deploy.

O que eu não consigo fazer por você (exige login nas suas contas): criar o projeto no
Supabase, criar os dois serviços no Render, e cadastrar os secrets no GitHub. O passo a
passo abaixo é exatamente o que clicar.

### 1. Banco (Supabase)

1. Crie um projeto novo no Supabase.
2. Em **Project Settings → Database**, copie a **connection string direta** (porta `5432`,
   não a do pooler/PgBouncer na `6543`). O DbUp usa `pg_advisory_lock` para coordenar as
   migrations, e o Npgsql usa prepared statements por padrão — nenhum dos dois é confiável
   no modo *transaction* do PgBouncer. Com o volume deste projeto, a conexão direta não tem
   nenhuma desvantagem prática.
3. Monte a connection string no formato do Npgsql, com SSL (o Supabase exige):
   ```
   Host=db.<seu-projeto>.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=<sua-senha>;SSL Mode=Require;Trust Server Certificate=true
   ```
   As migrations rodam sozinhas no startup da API (`DatabaseMigrator`) — não precisa aplicar
   o schema manualmente.

### 2. Serviços (Render)

Crie **dois** Web Services, ambos como **"Existing Image"** (não "Build from a repository" —
quem builda é o GitHub Actions):

**`sabemi-api`**
- Imagem: `ghcr.io/<seu-usuario>/<repo>-api:latest`
- Health check path: `/health`
- Variáveis de ambiente:
  | Nome | Valor |
  |---|---|
  | `ConnectionStrings__Default` | a connection string do Supabase (passo 1.3) |
  | `Webhook__ApiKey` | uma chave real, gerada por você — **não** `dev-local-key` |
  | `Processing__SimulatedDelayMs` | `2000` (opcional, é o default) |
- `PORT` **não precisa ser configurado** — o Render injeta automaticamente e o Dockerfile já
  lê `$PORT` (ver `src/SabemiTec.Api/Dockerfile`).
- Depois de criado, copie a URL pública (ex.: `https://sabemi-api.onrender.com`) — o próximo
  serviço depende dela.

**`sabemi-web`**
- Imagem: `ghcr.io/<seu-usuario>/<repo>-web:latest`
- Variável de ambiente:
  | Nome | Valor |
  |---|---|
  | `API_ORIGIN` | a URL pública do `sabemi-api` (ex.: `https://sabemi-api.onrender.com`) |
- O nginx dentro da imagem usa essa variável para fazer proxy de `/api` e `/webhooks` até a
  API — o mesmo mecanismo que evita CORS localmente (`web/nginx.conf.template`), só que
  apontando para uma URL pública em vez do nome do serviço no docker-compose.

Em ambos os serviços, pegue a **Deploy Hook URL** em *Settings → Deploy Hook* — é o que o
GitHub Actions vai chamar a cada push.

**Visibilidade do pacote no GHCR**: por padrão o GHCR publica os pacotes como privados. Ou
você torna as duas imagens públicas (*Package settings → Change visibility*, mais simples
para um projeto de avaliação), ou configura uma credencial de registry no Render apontando
para o `ghcr.io` com um PAT com escopo `read:packages`.

### 3. Secrets no GitHub

Em *Settings → Secrets and variables → Actions* do repositório:

| Secret | Valor |
|---|---|
| `RENDER_DEPLOY_HOOK_API` | Deploy Hook do serviço `sabemi-api` |
| `RENDER_DEPLOY_HOOK_WEB` | Deploy Hook do serviço `sabemi-web` |

`GITHUB_TOKEN` (usado para publicar no GHCR) já existe automaticamente em todo repositório —
não precisa criar.

A partir daí, todo push em `main` builda, testa e redeploya sozinho.

## Estrutura

```
src/SabemiTec.Api/
├── ACL/PartnerBank/        # fronteira com o payload do banco parceiro
├── Enum/                   # enums-como-classe (Enumeration + derivados)
├── Features/
│   ├── Webhooks/           # POST /webhooks/pagamento — ingestão + idempotência
│   ├── Processing/         # worker: claim, delay, upsert, retry/dead-letter
│   └── Dashboard/          # GET /api/payments — leitura, filtros, stats
├── Database/PostgreSQL/    # IUnitOfWork, Dapper, SQL, migrations (DbUp)
├── Middlewares/            # ApiKeyAuthMiddleware
└── Configurations/         # DI, RateLimiting, mapeamento de rotas

web/src/
├── api/                    # client, chamadas, tipos
├── hooks/                  # TanStack Query + filtros na URL
├── components/             # tabela, filtros, badges, stats
└── pages/Dashboard.tsx

tests/SabemiTec.Tests/
├── Unit/                   # ACL, validação
└── Integration/            # Testcontainers + WebApplicationFactory
```
