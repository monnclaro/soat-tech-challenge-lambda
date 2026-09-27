# soat-tech-challenge-lambda

> Autenticação serverless por CPF e API Gateway da oficina. Nasceu na Fase 3, apontando para 1 API monolítica, e passou a rotear para os 3 microsserviços da Fase 4: [os-service](https://github.com/monnclaro/soat-tech-challenge-os-service) · [billing-service](https://github.com/monnclaro/soat-tech-challenge-billing-service) · [execution-service](https://github.com/monnclaro/soat-tech-challenge-execution-service) · [infra-k8s](https://github.com/monnclaro/soat-tech-challenge-infra-k8s) · [infra-database](https://github.com/monnclaro/soat-tech-challenge-infra-database) · **lambda** (este repositório).

## Propósito

Uma AWS Lambda function + um API Gateway HTTP API que expõem o login do cliente da oficina por CPF:

- **AuthFunction** (`POST /auth/login-cpf`): valida o CPF informado, consulta existência/status (`Cliente.Ativo`) diretamente no RDS e devolve um JWT — a Function Serverless completa exigida pelo enunciado ("validar o CPF, consultar existência/status, gerar e devolver um token"), numa função só. Quem autentica aqui é `Cliente`, não `Usuario`: o token emitido só abre as rotas que fazem sentido pro cliente (consultar as próprias ordens de serviço, aprovar/reprovar o próprio orçamento) — funcionário continua logando por email/senha, sem relação com isto.

O token emitido aqui usa o **mesmo formato** aceito pelo `AddJwtAuthentication` do app principal (claims `Name`/`Role`, HS256), com role `Cliente` — diferente da role `Admin` usada pelo login de `Usuario`. Além de `Name`/`Role`, o token carrega `NameIdentifier` (Id do cliente) e uma claim customizada `documento` — usados pelo app pra confirmar que quem chama é dono do recurso antes de deixar aprovar/reprovar orçamento ou listar ordens de serviço de outro cliente (ver `OrdemServicosController` no app, e [RFC 0003](https://github.com/monnclaro/soat-tech-challenge/blob/main/docs/rfcs/0003-estrategia-de-autenticacao.md)). A validação do token nas rotas protegidas acontece na própria API (já existente desde a Fase 1) — não há Lambda Authorizer neste repositório: seria uma segunda validação redundante do mesmo JWT, não exigida pelo enunciado.

## Tecnologias

| Componente | Tecnologia |
|---|---|
| Runtime | .NET 8 (`dotnet8`, managed runtime da AWS — ver nota de versão abaixo) |
| Gateway | AWS API Gateway (HTTP API) |
| Persistência | Npgsql direto (sem EF Core — cold start menor) contra o RDS do infra-database |
| Segredos | SSM Parameter Store (`SecureString`) — passados como variável de ambiente da Lambda, sem chamada de rede em runtime |
| IaC | Terraform ~> 1.9 (pasta `infra/`) |
| Testes | xUnit (`tests/Shared.Tests`) |
| CI/CD | GitHub Actions (credenciais estáticas de sessão — AWS Academy) |

### Nota de versão: por que .NET 8 e não .NET 9

O app principal roda em .NET 9. Este repositório fica em **.NET 8 (LTS)** porque a AWS só publica managed runtime do Lambda para versões LTS do .NET — `dotnet9` não existe como valor válido de `runtime` (confirmado via `terraform validate`, que rejeita o valor). Detalhes no ADR "Lambda em .NET 8 (LTS), independente da versão do app".

### Nota: AWS Academy e prioridade de custo

Sem Secrets Manager (troca por SSM `SecureString`, grátis), sem IAM role própria (a função usa a `LabRole` já existente via `var.lab_role_arn` — o Academy bloqueia criação de roles), e as 3 integrações `/{servico}/api/{proxy+}` apontam pro **IP público de um node do EKS + NodePort** de cada microsserviço, não pra um ALB (que custaria ~$16-20/mês). Trade-off: esse IP pode mudar se o node for substituído — o CI/CD de qualquer um dos 3 microsserviços republica o parâmetro SSM a cada deploy, mas pode ser necessário um novo `terraform apply` deste repo depois de um deploy que trocou o node. Detalhes: ADR "Prioridade de custo e AWS Academy" (`soat-tech-challenge/docs/adr`).

## Arquitetura

```
Cliente da oficina
     │
     ▼
┌───────────────────────────────── API Gateway (HTTP API) ─────────────────────────────────┐
│                                                                                              │
│  POST /auth/login-cpf        ANY /os/api/{proxy+}   ANY /billing/api/{proxy+}   ANY /execucao/api/{proxy+}
│       │                              │                        │                          │
│       ▼                              ▼                        ▼                          ▼
│  ┌───────────┐              (mesmo node group EKS, NodePort por microsserviço — sem      │
│  │AuthFunction│               authorizer no Gateway, cada API valida o JWT sozinha)       │
│  └─────┬─────┘                 30081 → OS Service   30082 → Billing   30083 → Execução    │
│        │ valida CPF, consulta                                                              │
│        │ Cliente no RDS, gera JWT                                                          │
│        ▼                                                                                    │
│  SSM SecureString (jwt secret)                                                              │
│  RDS (infra-database)                                                                       │
└──────────────────────────────────────────────────────────────────────────────────────────┘
```

Diagrama de sequência completo do fluxo de autenticação: [soat-tech-challenge/docs/sequence-auth-cpf.md](https://github.com/monnclaro/soat-tech-challenge/blob/main/docs/sequence-auth-cpf.md).

## Estrutura

```
src/
├── Shared/              ← CpfValidator, JwtService, ClienteRepository, LoginCpfService (lógica testável)
└── AuthFunction/         ← handler Lambda (POST /auth/login-cpf)
tests/Shared.Tests/       ← testes unitários da lógica de autenticação (sem depender de AWS)
infra/                    ← Terraform: Lambda, API Gateway
```

## Execução e testes locais

```bash
dotnet restore
dotnet build
dotnet test
```

## Deploy

```bash
# 1. Empacotar
dotnet publish src/AuthFunction/AuthFunction.csproj -c Release -o publish/auth
cd publish/auth && zip -r ../../artifacts/auth-function.zip . && cd ../..

# 2. Provisionar
cd infra
terraform init
terraform apply \
  -var="lab_role_arn=arn:aws:iam::<account-id>:role/LabRole" \
  -var="auth_function_zip=../artifacts/auth-function.zip"
```

### Ordem de deploy entre os 4 repositórios

Este repositório depende de parâmetros SSM publicados pelos outros três. Ordem necessária na primeira subida do ambiente:

1. **infra-k8s** — cria VPC + EKS, publica `/soat/producao/network/*`, **o segredo JWT** (`/soat/producao/jwt/secret`) e o RabbitMQ compartilhado — o JWT é criado ali, não aqui, justamente pra evitar uma dependência circular: os microsserviços precisam do segredo antes de existir; o lambda precisa do IP do node, que só existe depois do primeiro deploy de algum microsserviço. Ver `soat-tech-challenge-infra-k8s/jwt.tf`.
2. **infra-database** — consome a VPC, cria o RDS (com os bancos lógicos `soat_os`/`soat_billing`), publica `/soat/producao/rds/*`.
3. **os-service / billing-service / execution-service** — cada um faz deploy no EKS já existente, lendo o segredo JWT (e RDS/Mongo/RabbitMQ conforme o caso) do SSM; o CI/CD de qualquer um deles publica `/soat/producao/app/node-ip` (IP público de um node do cluster compartilhado).
4. **lambda** (este repositório) — só consome: lê o segredo JWT e o IP do node do SSM, cria o API Gateway com as 3 rotas já apontando pra lá.

Depois do bootstrap inicial, cada repo aplica de forma independente — exceto que um deploy de microsserviço que troque o node ativo pode exigir reaplicar este repositório (ver nota de custo acima).

## API

- Rotas: `POST /auth/login-cpf` (público), `ANY /os/api/{proxy+}` (→ OS Service, NodePort 30081), `ANY /billing/api/{proxy+}` (→ Billing Service, NodePort 30082), `ANY /execucao/api/{proxy+}` (→ Execução Service, NodePort 30083) — cada uma repassada direto pro node do EKS, com o prefixo (`/os`, `/billing`, `/execucao`) removido e `/api` reposto antes de chegar no backend; a autorização é responsabilidade de cada API.
- Collection Postman com exemplos de login por CPF: [collection.json](https://github.com/monnclaro/soat-tech-challenge/blob/main/collection.json) do repositório do monolito (mesma collection, seção "Auth CPF" — histórica, mas o formato de token e o fluxo de login continuam os mesmos).

## CI/CD

[.github/workflows/ci-cd.yml](.github/workflows/ci-cd.yml): build + testes .NET → `dotnet publish`/zip da função → `terraform plan` (PR) ou `terraform apply` (push em `main`), reaproveitando o zip publicado como artifact do job de build. Segredos necessários: `AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY`, `AWS_SESSION_TOKEN` (do Academy, expiram com a sessão), `AWS_LAB_ROLE_ARN`.

## Links

- Diagrama de componentes completo, ADRs e RFCs: [soat-tech-challenge/docs](https://github.com/monnclaro/soat-tech-challenge/tree/main/docs)
