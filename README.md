# Rinha de Backend 2025 - Payment Processor

Este é um backend desenvolvido em C# (.NET 9) para processar pagamentos de forma assíncrona e eficiente, utilizando uma arquitetura baseada em filas e múltiplas instâncias para alta disponibilidade.

## Tecnologias Utilizadas

- **Linguagem**: C# (.NET 9)
- **Framework Web**: ASP.NET Core Minimal APIs
- **Banco de Dados**: PostgreSQL 16
- **Cache/Fila**: Redis
- **ORM**: Dapper
- **Load Balancer**: Nginx
- **Containerização**: Docker & Docker Compose

## Arquitetura

O sistema implementa uma arquitetura de microserviços com as seguintes camadas:

### Componentes Principais:

1. **Load Balancer (Nginx)**: Distribui requisições entre as instâncias da API
2. **API Instances (2x)**: 
   - **app1 (default)**: Processador principal com taxa de 5%
   - **app2 (fallback)**: Processador de fallback com taxa de 8%
3. **Redis**: Sistema de filas para processamento assíncrono
4. **PostgreSQL**: Persistência dos dados de pagamentos
5. **Background Jobs**: Consumer assíncrono para processar pagamentos da fila

### Fluxo de Processamento:

1. Cliente envia requisição de pagamento via API
2. Pagamento é validado e adicionado à fila Redis
3. Consumer background processa pagamentos assincronamente
4. Sistema tenta processador default primeiro, fallback em caso de falha
5. Health checks respeitam limite de 1 chamada por 5 segundos

## Endpoints da API

### POST /payments
Recebe solicitações de pagamento para processamento assíncrono.

**Request Body:**
```json
{
  "correlationId": "uuid",
  "amount": 100.50
}
```

**Response:** `202 Accepted`

### GET /payments-summary
Retorna resumo dos pagamentos processados por período.

**Query Parameters:**
- `from` (opcional): Data de início (DateTimeOffset)
- `to` (opcional): Data de fim (DateTimeOffset)

**Response:**
```json
{
  "default": {
    "totalRequests": 150,
    "totalAmount": 15000.00
  },
  "fallback": {
    "totalRequests": 25,
    "totalAmount": 2500.00
  }
}
```

### GET /payments/service-health
Health check da aplicação.

## Configuração e Execução

### Pré-requisitos
- Docker
- Docker Compose

### Como Executar

```bash
# Clonar o repositório
git clone https://github.com/antonioprudente/rinha-backend-2025
cd rinha-backend

# Subir toda a infraestrutura
docker-compose up -d
```

O sistema ficará disponível em `http://localhost:9999`

### Limitações de Recursos

Cada instância da API está configurada com:
- **CPU**: 0.35 cores
- **Memória**: 90MB

## Estrutura do Projeto

```
payment-processor/
├── Program.cs              # Configuração da API e endpoints
├── Jobs/
│   └── Consumer.cs         # Background job para processar fila
├── Model/
│   ├── Payment.cs          # Modelo de pagamento
│   ├── PaymentProcessorRequest.cs
│   └── PaymentSummaryDto.cs
├── Repository/
│   ├── IPaymentRepository.cs
│   └── PaymentRepository.cs # Acesso aos dados com Dapper
└── Services/
    └── PaymentProcessorService.cs # Integração com processadores
```

## Estratégia de Fallback

O sistema implementa uma estratégia inteligente:

1. **Processador Default** (taxa 5%): Primeira opção para todos os pagamentos
2. **Processador Fallback** (taxa 8%): Usado quando o default falha
3. **Health Checks**: Monitoramento respeitando rate limit
4. **Processamento Assíncrono**: Usando Redis como fila para alta performance

## Monitoramento

- Health check disponível em `/payments/service-health`
- Logs detalhados da aplicação
- Métricas de summary por processador

