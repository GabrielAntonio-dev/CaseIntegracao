# Case Técnico — Integração de Sistemas (Pedidos → CRM)

API em **C# / ASP.NET Core (.NET 9)** que recebe webhooks de um **sistema de pedidos** e sincroniza clientes e pedidos em um **CRM** (mock embutido), tratando:

- eventos fora de ordem
- falhas temporárias na API de destino
- reenvios duplicados do mesmo evento

---

## Especificação do problema

### Contexto

O sistema de pedidos dispara notificações (webhook) quando um pedido é criado, atualizado ou cancelado. O CRM precisa manter clientes e pedidos sincronizados via REST.

Este serviço é o **integrador**: recebe o webhook, garante o cliente no CRM e cria/atualiza/cancela o pedido correspondente.

### Requisitos funcionais atendidos

1. Endpoint HTTP para receber o webhook do sistema de pedidos.
2. Se o cliente do pedido não existir no CRM, **criar o cliente** antes de sincronizar o pedido.
3. Criar ou atualizar o pedido no CRM conforme o tipo de evento.
4. **Idempotência:** processar o mesmo evento mais de uma vez não duplica nem corrompe o estado.
5. **Ordem de eventos:** o estado final no CRM/projeção reflete o estado **mais recente** (`occurredAt`), não necessariamente o último evento que chegou.
6. **Falha no CRM** (5xx, 429, timeout): retry com backoff, sem perder o evento e sem martelar a API.

### Contrato do webhook

`POST /api/webhooks/orders`

```json
{
  "eventId": "evt_01HXYZ",
  "eventType": "order.created",
  "occurredAt": "2026-07-27T18:00:00Z",
  "data": {
    "orderId": "ord_123",
    "status": "pending",
    "totalAmount": 150.90,
    "currency": "BRL",
    "customer": {
      "externalId": "cust_456",
      "name": "Ana Silva",
      "email": "ana@email.com",
      "document": "12345678901"
    }
  }
}
```


| Campo         | Uso                                                                                                                     |
| ------------- | ----------------------------------------------------------------------------------------------------------------------- |
| `eventId`     | Chave de idempotência (obrigatório no body; ou via header `X-Idempotency-Key` se o body vier sem `eventId`)             |
| `eventType`   | `order.created`                                                                                                         |
| `occurredAt`  | Relógio do domínio para ordenação                                                                                       |
| `data.status` | Status do pedido (`pending`, `confirmed`, `canceled`). Em `order.canceled`, o status vira **cancelado automaticamente** |


Cada evento novo precisa de um `eventId` **diferente**. Update/cancel usam o mesmo `orderId` com `occurredAt` mais recente.

### CRM mock

Mesmo host, prefixo `/crm`:

- `GET/POST/PUT /crm/customers`
- `GET/POST/PUT /crm/orders`

Simula ~15% de falhas (`CrmMock:FailureRate`) com 429, 500 e timeout real (delay `CrmMock:TimeoutDelaySeconds`, padrão 12s, acima do `HttpClient.Timeout` de 10s). Persistência em `data/crm/*.json`. Os endpoints `/crm` aparecem no Swagger junto com o restante da API.

---

## Como rodar o projeto

Pré-requisito: [.NET 9 SDK](https://dotnet.microsoft.com/download).

```bash
cd CaseIntegracao
dotnet restore
dotnet build
dotnet test
dotnet run --project CaseIntegracao.Api
```

- API: [http://localhost:5080](http://localhost:5080)
- Swagger: [http://localhost:5080/swagger](http://localhost:5080/swagger)

Collection Postman: `[postman/CaseIntegracao.postman_collection.json](postman/CaseIntegracao.postman_collection.json)`  
Exemplos HTTP: `[CaseIntegracao.Api/CaseIntegracao.Api.http](CaseIntegracao.Api/CaseIntegracao.Api.http)`

### Endpoints principais


| Método | Rota                    | Descrição                                               |
| ------ | ----------------------- | ------------------------------------------------------- |
| POST   | `/api/webhooks/orders`  | Recebe evento do sistema de pedidos                     |
| GET    | `/api/events`           | Lista eventos e status de processamento                 |
| GET    | `/api/events/{eventId}` | Detalhe de um evento                                    |
| GET    | `/api/orders/{orderId}` | Projeção local do pedido (estado mais recente aplicado) |
| GET    | `/api/metrics`          | Contadores de sync / falha / retry / idempotência       |
| *      | `/crm/...`              | API mock do CRM                                         |


### Configuração (`appsettings.json`)


| Chave                                        | Significado                                                      |
| -------------------------------------------- | ---------------------------------------------------------------- |
| `Storage:DataPath`                           | Pasta dos JSON (`data/`)                                         |
| `Crm:BaseUrl`                                | Base do CRM (em dev: mesmo host)                                 |
| `CrmMock:FailureRate`                        | Taxa de falha do mock (`0` desliga)                              |
| `CrmMock:TimeoutDelaySeconds`                | Delay do modo timeout do mock (deve ser > timeout do HttpClient) |
| `Retry:MaxAttempts`                          | Tentativas antes de carta morta                                  |
| `Retry:BaseDelaySeconds` / `MaxDelaySeconds` | Backoff                                                          |
| `Retry:PollIntervalSeconds`                  | Intervalo do worker de retry                                     |


---

## Decisões de arquitetura (e por quê)

### Organização em camadas (DDD + SOLID)


| Projeto                         | Responsabilidade                                                  |
| ------------------------------- | ----------------------------------------------------------------- |
| `CaseIntegracao.Domain`         | Entidades, enums, políticas e portas (`ICrmClient`, repositories) |
| `CaseIntegracao.Application`    | Casos de uso (`EventoPedidoProcessor`, `ConsultaEventosService`)  |
| `CaseIntegracao.Infrastructure` | Arquivo JSON, `CrmHttpClient`, Polly, métricas                    |
| `CaseIntegracao.Api`            | Controllers, Swagger, CRM mock, `RetryBackgroundService`          |
| `CaseIntegracao.Tests`          | Testes de idempotência, ordenação e retry                         |


Isso separa regra de negócio de transporte/persistência e facilita testar o processador sem HTTP real.

### Persistência em arquivo

O case permite banco local ou arquivo. Escolhemos **JSON em disco** para:

- zero dependência externa (sem Docker/DB)
- entrega alinhada ao esforço estimado (~3h)
- auditoria simples dos eventos

Arquivos:

- `data/events.json` — eventos e status de sync
- `data/orders.json` — projeção local do pedido
- `data/crm/*.json` — store do mock

## Critérios de avaliação — como a solução atende

### Modelagem (estado interno)

- `EventoIntegracao`**:** `eventId`, tipo, payload, `StatusProcessamentoEvento` (`Recebido`, `Processando`, `Sincronizado`, `Falhou`, `CartaMortua`, `IgnoradoObsoleto`), `AttemptCount`, `NextRetryAt`, `LastError`.
- `ProjecaoPedido`**:** estado local do pedido (`StatusPedido`, valores, cliente, `LastOccurredAt`) usado para decidir se um evento deve ser aplicado.
- Status de processamento ≠ status de negócio do pedido: um é pipeline de sync; o outro é o pedido no CRM.

### Tratamento de erro (CRM falha)

Não engole e não derruba o processo:

1. Polly no `HttpClient`: retries curtos + **circuit breaker** (5 falhas → abre 15s).
2. Falha transitória → evento `Falhou`, agenda `NextRetryAt` com **exponential backoff + jitter**.
3. Worker reprocessa eventos vencidos.
4. Após `MaxAttempts` → `CartaMortua` (evento **não é perdido**; fica auditável em `GET /api/events`).
5. Logging estruturado em sucessos e falhas.

### Idempotência

Chave = `eventId`. Se o evento já está em sucesso terminal (`Sincronizado` / `IgnoradoObsoleto`), a API responde sem novo efeito no CRM.

**Como provar:** enviar o mesmo JSON duas vezes no Swagger → segunda resposta com `alreadyProcessed: true`; CRM/projeção sem duplicata. Coberto em teste automatizado.

### Ordem de eventos

Compara `occurredAt` do evento com `ProjecaoPedido.LastOccurredAt`:

- se o evento for **mais antigo** → marca `IgnoradoObsoleto` e **não** sobrescreve CRM/projeção;
- se for **mais novo ou igual** → aplica e sincroniza.

Assim o estado final reflete o momento de negócio mais recente, mesmo com `order.updated` chegando antes de `order.created`.

### Código

- Nomes de negócio em PT-BR; sufixos técnicos em inglês (`Repository`, `Service`, `Controller`).
- Contrato HTTP/JSON do webhook em inglês (padrão de integração).
- Responsabilidades claras por camada; políticas de domínio isoladas (`PoliticaOrdenacaoEventos`).

---

## Diferenciais implementados

- Circuit breaker + exponential backoff nas chamadas ao CRM
- Métricas simples em `GET /api/metrics`
- Reflexão de escala com fila (abaixo)

### Como escalaria com fila

O webhook validaria o payload, persistiria o evento (inbox) e publicaria em fila (SQS / RabbitMQ / Azure Service Bus). Workers competiriam pelas mensagens com visibility timeout e DLQ nativa. Idempotência e projeção ficariam em store compartilhado (DB). Isso desacopla ingestão de sync e melhora throughput/resiliência entre instâncias.

---

## O que deixaria diferente com mais tempo

- Banco (ou outbox/inbox) em vez de arquivo, para concorrência e múltiplas instâncias
- Autenticação nos endpoints com Bearer
- OpenTelemetry / tracing distribuído
- Documentar OpenAPI com exemplos por `eventType`

---

## Testes

```bash
dotnet test
```

- reenvio do mesmo `eventId` não duplica efeito
- evento antigo após um mais novo não sobrescreve o estado
- falha transitória no CRM agenda retry e sincroniza depois
- `order.canceled` força status cancelado

---

## Uso de AI

- Criação do readme, garantindo uma maior cobertura de informações;
- Criação dos cenários de testes unitários, garantindo uma cobertura mais acertiva dos pontos chaves da integração.

