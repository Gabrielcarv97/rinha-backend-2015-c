# Rinha de Backend 2025 - Payment Processor

Este é um backend desenvolvido em C# (.NET 9) para intermediar pagamentos entre clientes e processadores de pagamento (Payment Processors), implementando estratégias de fallback e otimização de taxas.

## Tecnologias Utilizadas

- **Linguagem**: C# (.NET 9)
- **Framework Web**: ASP.NET Core Minimal APIs
- **Banco de Dados**: PostgreSQL
- **ORM**: Dapper
- **Load Balancer**: Nginx
- **Conteinerização**: Docker

## Arquitetura

O sistema é composto por:

1. **Load Balancer (Nginx)**: Distribui requisições entre duas instâncias do backend
2. **Backend APIs (2 instâncias)**: Processa pagamentos e gerencia integração com Payment Processors
3. **Banco de Dados (PostgreSQL)**: Armazena dados dos pagamentos processados

## Estratégia de Processamento

O backend implementa uma estratégia inteligente de processamento:

1. **Prioriza o Payment Processor Default** (taxa menor - 5%)
2. **Fallback para Payment Processor Fallback** (taxa maior - 8%) quando necessário
3. **Health Check** dos processadores respeitando limite de 1 chamada/5s
4. **Processamento assíncrono** usando Channels para alta performance

## Endpoints

- `POST /payments`: Recebe solicitações de pagamento
- `GET /payments-summary`: Retorna resumo dos pagamentos processados por período

## Como Executar

```bash
# Subir os Payment Processors primeiro (para criar a rede)
cd payment-processor/
docker-compose -f docker-compose.yml up -d

# Subir o backend
docker-compose up -d
```

O sistema ficará disponível em `http://localhost:9999`

## Repositório do Código Fonte

[https://github.com/antonioprudente/rinha-backend-2025](https://github.com/antonioprudente/rinha-backend-2025)
