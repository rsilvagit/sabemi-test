# Sabemi TEC — Serviço de Webhooks de Pagamento

Avaliação de habilidades técnicas: um serviço que recebe notificações de pagamento de um
banco parceiro, garante que nenhuma seja processada duas vezes, processa em background e
exibe o resultado num painel administrativo.

## Como rodar

Pré-requisito: Docker Desktop.

```bash
docker compose up -d --build
```

Isso sobe três serviços — Postgres, API e o dashboard (servido por nginx). As migrations
não rodam mais sozinhas: aplique-as manualmente antes do primeiro uso (veja
`Database/PostgreSQL/Migrations/DatabaseMigrator.cs`) e o resto fica pronto:

- **Dashboard**: http://localhost:5173
- **API**: http://localhost:8080 (Swagger em `/swagger`)
- **ApiKeys de teste** (chaves separadas — veja "Autenticação" abaixo): webhook
  `dev-local-webhook-key`, dashboard `dev-local-dashboard-key` (header `X-Api-Key`; a do
  dashboard é configurável em `.env` a partir de `.env.example`)

Para gerar dados de exemplo, use a collection `requests/webhooks.http` (abre direto no
VS Code/Rider) ou:

```bash
curl -X POST http://localhost:8080/webhooks/payment \
  -H "Content-Type: application/json" -H "X-Api-Key: dev-local-webhook-key" \
  -d '{"id_transacao":"TX-001","id_contrato":"CT-42","valor":150.50,"data_pagamento":"2026-09-15T10:00:00Z","status":"PAGO"}'
```

O evento aparece no dashboard como **Pendente** e muda sozinho para **Sucesso** ~2 segundos
depois — é o processamento assíncrono em ação.

### Carga sintética (opcional)

Pra ver o dashboard [em produção](https://sabemi-web.onrender.com) se movendo sozinho em vez
de gerar eventos manualmente, rode localmente o worker `load-simulator` — ele faz o papel do
banco parceiro, disparando um **lote de transações concorrentes** (`CONCURRENT_REQUESTS`, 5
por padrão) a cada 25s contra `POST /webhooks/payment`, contra a API de staging no Render.
Busca a lista de contratos reais em `GET /webhooks/contracts` no startup (com retry, já que
a API pode não estar pronta ainda) em vez de hardcoded, pra nunca divergir do que está
seedado — mesmos dados de `GET /api/payments/contracts` (usada pelo filtro do dashboard),
espelhados sob `/webhooks` pra não depender de `Dashboard:ApiKey`: o worker só precisa da
chave do webhook, a mesma que já usa pra postar os pagamentos. Mistura payloads válidos
(`PAGO`/`FALHA`) e inválidos — `id_transacao` ausente, `status` desconhecido — pra exercitar
o caminho de erro de validação. Sem cenário de valor negativo: é um dashboard de liquidação
de parcela, não de movimentação de conta, então não existe "saque" nesse domínio.

```bash
STG_WEBHOOK_API_KEY=<chave real do Webhook__ApiKey do sabemi-api, não a do dashboard> \
  docker compose --profile simulator up -d --build
```

Roda no seu próprio computador, não precisa de um serviço pago rodando 24/7 no Render — só
liga quando você quiser demonstrar o sistema com dados em movimento, e desliga com `docker
compose --profile simulator down` quando terminar. Pra mirar na API local em vez da de
staging, defina `LOAD_SIMULATOR_API_URL=http://api:8080` também; pra ajustar o intervalo
entre lotes, `LOAD_SIMULATOR_INTERVAL_SECONDS=<n>`; pra ajustar quantas transações
simultâneas por lote, `LOAD_SIMULATOR_CONCURRENCY=<n>`.

É opt-in (`--profile simulator`) porque não é parte do requisito — só ajuda a demonstrar o
sistema. Um `docker compose up` normal não sobe esse worker.

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
| `POST /webhooks/payment` | Não | Um único INSERT; abrir transação seria cerimônia |
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

### Rate limiting: token bucket no `/webhooks`, fixed window no dashboard

Notificação bancária chega em rajada (o parceiro fecha um lote e dispara tudo de uma vez),
não em fluxo constante — por isso token bucket, e não fixed/sliding window, que penalizariam
a rajada legítima. `QueueLimit = 0` é deliberado: enfileirar significa segurar a requisição do
banco esperando token, contradizendo o requisito de responder rápido — melhor 429 imediato do
que segurar por segundos.

`GET /api/payments` usa uma política separada (`DashboardPolicy`, fixed window, 120
req/min por IP): o padrão de tráfego é outro — polling humano de uma aba de browser, não
rajada de parceiro — e partitionar por ApiKey não faria sentido aqui, já que todo cliente do
dashboard compartilha a mesma chave (ver seção de autenticação abaixo).

### Duas ApiKeys, uma pro webhook e outra pro dashboard

`ApiKeyAuthMiddleware` protege os dois grupos de rota (`/webhooks/payment` e
`/api/payments*`), mas com **chaves diferentes** — `Webhook:ApiKey` e `Dashboard:ApiKey`,
mesmo header `X-Api-Key`. Antes as duas rotas compartilhavam a mesma chave; separadas
porque a chave do dashboard inevitavelmente acaba do lado do cliente (bakeada no bundle JS
em produção, ver abaixo), então um vazamento dela nunca deve dar acesso a `POST
/webhooks/payment` — só o banco parceiro deve ter `Webhook:ApiKey`.

### O dashboard também exige ApiKey — local via proxy, no Render via CORS

`GET /api/payments` é dado de pagamento; publicado sem auth, qualquer um na internet lê
tudo. Localmente (docker-compose, `npm run dev`) o browser nunca vê a chave: o nginx do
serviço `web` (ou o proxy de dev do Vite) injeta o header `X-Api-Key` (a chave do
dashboard, `Dashboard:ApiKey`) em toda requisição proxied para `/api/`, lida em runtime via
`envsubst`/`vite.config.ts`.

No Render isso não é possível — ver a seção de arquitetura de deploy logo abaixo, que
explica por que o proxy nginx→API não funciona lá — então o browser chama a API
diretamente (`https://sabemi-api.onrender.com`, CORS liberado só para `GET` em
`/api/payments*` e só para a origem do `sabemi-web`) e manda a `X-Api-Key` ele mesmo, lida
de uma variável bakeada no bundle JS em build time (`VITE_API_KEY`, ver
`.github/workflows/deploy-web.yml`). Isso não é uma regressão de segurança real: a chave do
dashboard nunca foi um segredo forte (é só um filtro contra scraping casual, já era isso
mesmo quando só o proxy a conhecia), e a alternativa — desistir da auth no `GET` — seria
pior. É exatamente por essa exposição inevitável que ela precisa ser uma chave própria,
diferente da do webhook.

### Vertical slice, não Clean Architecture / DDD tático / hexagonal

`Features/<Webhooks|Processing|Dashboard>/` agrupa por caso de uso, não por camada técnica —
cada requisito do PDF mora inteiro numa pasta. Descartado deliberadamente:

- **Camadas por assembly** (`Api`/`Application`/`Infrastructure`) dariam uma implementação
  por abstração num serviço com um endpoint de escrita e dois de leitura.
- **DDD tático** (agregados, value objects) não se aplica: a única invariante de negócio
  (soma do contrato) é correta porque vive no `ON CONFLICT DO UPDATE` do SQL, não porque um
  agregado a protege.
- **Hexagonal** paga quando há múltiplos adaptadores de cada lado. Aqui há um driving (HTTP)
  e um driven (Postgres); a ACL já cumpre o papel de porta onde de fato existe um adaptador
  plausível (um segundo banco parceiro).

Convenção de DI: `Configurations/Extensions/ServicesExtensions.cs` concentra métodos `AddX`
por área, encadeados fluentemente no `Program.cs` — nenhum `services.AddScoped<>()` solto.

### Erro de validação: aba própria, fora da lista principal

O badge "Erro" cobria dois casos bem diferentes: payload malformado (nunca chegou a um
resultado de negócio, `id_transacao`/`id_contrato` podem estar ausentes ou incorretos) e
payload processado com sucesso mas rejeitado pelo banco (`payment_status = FALHA`, contrato
e transação confiáveis). Uma primeira versão resolveu isso com um campo derivado
`error_category` (`Validation` | `PaymentFailure`) rotulando o mesmo badge — mas misturar um
evento sem referência confiável de contrato numa lista de pagamentos, associado a um
`contract_id` como se fosse dado real, não fazia sentido. `GET /api/payments` (e os cards de
stats) agora **excluem** payload inválido (`where is_valid`) inteiramente; ele só aparece
via `GET /api/payments/invalid`, numa aba separada do dashboard ("Eventos inválidos"), sem
`LEFT JOIN` com `contract`, sem `effectiveStatus`/`errorCategory` — só os campos brutos e o
motivo da rejeição. O requisito do PDF ("alerta visual claro" pra erro de validação) continua
atendido, só não misturado com pagamentos de verdade.

### Dado de demonstração: tipo de contrato, parcela e valor total

O enunciado fala em "liquidação de seguros ou parcelas de empréstimos", mas o payload do
webhook não carrega tipo de contrato, número de parcelas nem valor total — só
`id_transacao, id_contrato, valor, data_pagamento, status`. Em vez de inventar isso por
evento (a mesma armadilha já descartada com um campo de "método de pagamento" que o banco
nunca envia), existe uma tabela `contract` **seedada manualmente** (migrations `0002`,
`0003`, `0004`) com 8 contratos fake, cada um com tipo (`Emprestimo`/`Seguro`), total de
parcelas e valor total — deixado explícito no código que é dado de demonstração, não algo
que o banco parceiro informa. Empréstimo e Seguro têm parcelamento (migration `0004`
adicionou parcelas também ao Seguro, que antes era só um prêmio único — inconsistente com
Empréstimo, sinalizado pelo usuário).

A partir daí, por `LEFT JOIN` com essa tabela:
- **Tipo de Contrato**, **Parcela** (`3/12`) e **Valor total do contrato** aparecem só no
  detalhamento expandido — não na tabela principal, pra manter a visão geral enxuta.
- O número da parcela é calculado com `row_number() over (partition by contract_id order by
  received_at)`, **em módulo do total de parcelas** (`((posição - 1) % installments) + 1`):
  sem o módulo, o número cresceria sem limite conforme o `load-simulator` gera tráfego
  contínuo pros mesmos 8 contratos (chegou a mostrar `262/24` antes do fix) — com módulo ele
  cicla de volta pro 1 depois do total, como um contrato "recomeçando".
- **`valor` de cada transação é consistente com o contrato**: `total_value / installments`
  pra Empréstimo/Seguro com parcelamento, `total_value` direto se não houver. Sem isso o
  `load-simulator` gerava um valor aleatório desconectado do contrato (ex.: uma "parcela" de
  R$ 53,46 num empréstimo de R$ 6.000/12, que não fecha matematicamente — sinalizado pelo
  usuário). `GET /api/payments/contracts` (dashboard) / `GET /webhooks/contracts` (worker) retornam o
  contrato completo (não só o ID) pra isso.
- Filtro de contrato: **dropdown** com os contratos reais, não texto livre — elimina digitar
  um ID errado ou inexistente.
- Filtro por **Tipo de Contrato** (Empréstimo/Seguro) na `FiltersBar`.

### Detalhamento expansível pra qualquer status, não só erro

Clicar em qualquer linha da tabela (Sucesso, Erro ou Pendente) expande um painel com os
dados que o banco mandou — cor do painel acompanha o status (verde/vermelho/âmbar). Antes só
linhas de erro expandiam; não tinha razão pra restringir, já que confirmar os dados de uma
transação bem-sucedida é tão útil quanto investigar uma com problema. `Processado em` saiu
da visão geral da tabela (ficava redundante ali) e só aparece no detalhamento, junto com
`Recebido em` pra dar o contexto completo do ciclo de vida do evento.

### Frontend: polling

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
| Autenticação de usuário no dashboard | Protege com a mesma ApiKey (via proxy nginx, não no bundle JS — ver seção de rate limiting/auth); em produção seria OIDC + BFF, um usuário humano por trás de uma ApiKey compartilhada não dá auditoria individual |
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

Pipeline dividido por aplicação, cada uma independente:

- `.github/workflows/ci.yml` — roda a suíte de testes a cada push em `main`. Não publica
  imagem nem faz deploy.
- `.github/workflows/deploy-api.yml`, `deploy-web.yml` — cada um é **manual**
  (`workflow_dispatch`, em *Actions → escolha o workflow → Run workflow*), autocontido: testa
  (só o da API), builda a imagem daquela aplicação, publica no GitHub Container Registry
  (`ghcr.io`) com a tag `:latest` e dispara o Deploy Hook do respectivo serviço no Render.
  Rodar o deploy de uma aplicação nunca builda nem redeploya a outra. O Render **não** builda
  a partir do repositório — ele só puxa a imagem já publicada.

O `SabemiTec.LoadSimulator` (ver "Carga sintética" acima) não tem workflow de deploy — ele
roda local, apontando pra API de staging, em vez de um serviço pago 24/7 no Render.

O que eu não consigo fazer por você (exige login nas suas contas): criar o projeto no
Supabase, criar os serviços no Render, e cadastrar os secrets no GitHub. O passo a passo
abaixo é exatamente o que clicar.

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
   O `DatabaseMigrator` não roda mais automaticamente no startup da API — aplique o schema
   manualmente contra essa connection string antes do primeiro deploy.

### 2. Serviços (Render)

Crie **dois** Web Services e, opcionalmente, **um** Background Worker, todos como
**"Existing Image"** (não "Build from a repository" — quem builda é o GitHub Actions):

**`sabemi-api`**
- Imagem: `ghcr.io/<seu-usuario>/<repo>-api:latest`
- Health check path: `/health`
- Sem `ASPNETCORE_ENVIRONMENT` configurado, o Render roda em `Production` por padrão, o que
  carrega `src/SabemiTec.Api/appsettings.Production.json` — esse arquivo é versionado e só
  declara as chaves com placeholder vazio (nunca o valor real); as env vars abaixo sempre
  sobrescrevem esses placeholders em runtime.
- Variáveis de ambiente:
  | Nome | Valor |
  |---|---|
  | `ConnectionStrings__Default` | a connection string do Supabase (passo 1.3) |
  | `Webhook__ApiKey` | uma chave real, gerada por você — **só o banco parceiro deve ter essa** |
  | `Dashboard__ApiKey` | outra chave real, diferente da de cima — essa vai parar num bundle JS público, então nunca deve dar acesso ao webhook |
  | `Processing__SimulatedDelayMs` | `2000` (opcional, é o default) |
  | `Cors__DashboardOrigins__0` | a URL pública do `sabemi-web` (ex.: `https://sabemi-web.onrender.com`) |
- `PORT` **não precisa ser configurado** — o Render injeta automaticamente e o Dockerfile já
  lê `$PORT` (ver `src/SabemiTec.Api/Dockerfile`).
- Depois de criado, copie a URL pública (ex.: `https://sabemi-api.onrender.com`) — o passo do
  secret `DASHBOARD_API_KEY` abaixo e o `sabemi-web` dependem dela.

**`sabemi-web`**
- Imagem: `ghcr.io/<seu-usuario>/<repo>-web:latest`
- Não precisa de variáveis de ambiente específicas — `API_ORIGIN`/`DASHBOARD_API_KEY` ficam
  nos defaults do Dockerfile (não são usados de verdade em produção, ver abaixo).
- **Antes de dar push**: cadastre o secret `DASHBOARD_API_KEY` no GitHub (mesmo valor do
  `Dashboard__ApiKey` do `sabemi-api` — **nunca** o `Webhook__ApiKey`) — o CI passa ele como
  build-arg pro `npm run build` do `sabemi-web`, bakeando no bundle JS. Sem isso a imagem
  builda com uma chave vazia e o dashboard não autentica. Ver por quê na seção "Deploy no
  Render" acima: o nginx do `sabemi-web` não consegue proxyar pro `sabemi-api` lá, então o
  browser chama a API diretamente e manda a chave ele mesmo.

Em ambos os serviços, pegue a **Deploy Hook URL** em *Settings → Deploy Hook* — é o que o
GitHub Actions vai chamar quando você rodar o workflow manual daquela aplicação.

Não é preciso criar nada no Render pro `SabemiTec.LoadSimulator` — ele não é deployado (um
Background Worker no Render não tem tier grátis, mínimo $7/mês), você roda ele localmente
contra o `sabemi-api` de staging quando quiser gerar carga. Ver "Carga sintética" acima.

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
| `DASHBOARD_API_KEY` | a mesma chave real usada em `Dashboard__ApiKey` no `sabemi-api` — bakeada no bundle do `sabemi-web` em build time. **Não** é o `Webhook__ApiKey` do banco parceiro. |

`GITHUB_TOKEN` (usado para publicar no GHCR) já existe automaticamente em todo repositório —
não precisa criar.

A partir daí, todo push em `main` roda a suíte de testes (`ci.yml`); o deploy de cada
aplicação é sempre manual, disparado por você em *Actions*.

## Estrutura

**Por que monorepo:** API, worker de carga sintética e dashboard vivem no mesmo repositório
e compartilham o mesmo pipeline de CI/CD (um workflow de deploy por aplicação, mas todos no
`.github/workflows/` deste repo). Isso é uma escolha deliberada **para este teste/PoC** —
simplifica avaliação e onboarding, um único `git clone` + `docker compose up` sobe tudo. Não
é a arquitetura que se levaria pra produção: lá, cada aplicação (`sabemi-api`, `sabemi-web`)
teria **repositório próprio**, pipeline de CI/CD independente, e **ambientes segregados** de
staging e produção (banco, secrets, URLs e Deploy Hooks distintos por ambiente — hoje só
existe um ambiente de demo/staging compartilhado, sem produção real).

```
src/SabemiTec.Api/
├── ACL/PartnerBank/        # fronteira com o payload do banco parceiro
├── Enum/                   # enums-como-classe (Enumeration + derivados)
├── Features/
│   ├── Webhooks/           # POST /webhooks/payment — ingestão + idempotência
│   ├── Processing/         # worker: claim, delay, upsert, retry/dead-letter
│   └── Dashboard/          # GET /api/payments(/stats|/contracts|/{id}) — leitura, filtros, stats
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
