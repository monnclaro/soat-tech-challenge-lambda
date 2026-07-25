# soat-tech-challenge-lambda

> Autenticação serverless por CPF e API Gateway da oficina — parte da Fase 3 do Tech Challenge FIAP: [app](https://github.com/monnclaro/soat-tech-challenge) · [infra-k8s](https://github.com/monnclaro/soat-tech-challenge-infra-k8s) · [infra-database](https://github.com/monnclaro/soat-tech-challenge-infra-database) · **lambda** (este repositório).

## Propósito

Duas AWS Lambda functions + um API Gateway HTTP API que protegem as rotas sensíveis da aplicação:

- **AuthFunction** (`POST /auth/login-cpf`): valida o CPF informado pelo cliente, consulta existência/status (`Cliente.Ativo`) diretamente no RDS e devolve um JWT.
- **AuthorizerFunction**: Lambda Authorizer (formato *simple response*) plugado nas rotas `/api/*` do Gateway — valida o mesmo JWT (HS256, segredo compartilhado via SSM `SecureString`) antes de deixar a requisição chegar ao node do EKS.

O token emitido aqui usa o **mesmo formato** aceito pelo `AddJwtAuthentication` do app principal (claims `Name`/`Role`, HS256) — só muda quem emite: usuários internos continuam logando pelo endpoint `POST /api/auth/login` do app (email/senha), clientes logam por CPF aqui.

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

Sem Secrets Manager (troca por SSM `SecureString`, grátis), sem IAM role própria (as duas funções usam a `LabRole` já existente via `var.lab_role_arn` — o Academy bloqueia criação de roles), e a integração `/api/{proxy+}` aponta pro **IP público de um node do EKS + NodePort**, não pra um ALB (que custaria ~$16-20/mês). Trade-off: esse IP pode mudar se o node for substituído — o CI/CD do repo da app republica o parâmetro SSM a cada deploy, mas pode ser necessário um novo `terraform apply` deste repo depois de um deploy da app que trocou o node. Detalhes: ADR "Prioridade de custo e AWS Academy" (`soat-tech-challenge/docs/adr`).

## Arquitetura

```
Cliente/Front
     │
     ▼
┌─────────────────────────── API Gateway (HTTP API) ───────────────────────────┐
│                                                                                │
│  POST /auth/login-cpf                     ANY /api/{proxy+}                   │
│       │                                        │                              │
│       ▼                                        ▼ (Lambda Authorizer)          │
│  ┌───────────┐                          ┌──────────────┐                     │
│  │AuthFunction│                          │AuthorizerFunc│                     │
│  └─────┬─────┘                          └──────┬───────┘                     │
│        │ valida CPF, consulta                  │ valida JWT (HS256)          │
│        │ Cliente no RDS                        │ ok? → HTTP_PROXY            │
│        ▼                                        ▼                              │
│  SSM SecureString (jwt secret)     IP público do node EKS:NodePort → soat-api │
│  RDS (infra-database)                                                         │
└────────────────────────────────────────────────────────────────────────────┘
```

Diagrama de sequência completo do fluxo de autenticação: [soat-tech-challenge/docs/sequence-auth-cpf.md](https://github.com/monnclaro/soat-tech-challenge/blob/main/docs/sequence-auth-cpf.md).

## Estrutura

```
src/
├── Shared/              ← CpfValidator, JwtService, ClienteRepository, LoginCpfService (lógica testável)
├── AuthFunction/         ← handler Lambda (POST /auth/login-cpf)
└── AuthorizerFunction/   ← handler Lambda (Authorizer do API Gateway)
tests/Shared.Tests/       ← testes unitários da lógica de autenticação (sem depender de AWS)
infra/                    ← Terraform: Lambdas, API Gateway
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
dotnet publish src/AuthorizerFunction/AuthorizerFunction.csproj -c Release -o publish/authorizer
cd publish/authorizer && zip -r ../../artifacts/authorizer-function.zip . && cd ../..

# 2. Provisionar
cd infra
terraform init
terraform apply \
  -var="lab_role_arn=arn:aws:iam::<account-id>:role/LabRole" \
  -var="auth_function_zip=../artifacts/auth-function.zip" \
  -var="authorizer_function_zip=../artifacts/authorizer-function.zip"
```

### Ordem de deploy entre os 4 repositórios

Este repositório depende de parâmetros SSM publicados pelos outros três. Ordem necessária na primeira subida do ambiente:

1. **infra-k8s** — cria VPC + EKS, publica `/soat/producao/network/*`.
2. **infra-database** — consome a VPC, cria o RDS, publica `/soat/producao/rds/*`.
3. **app** (`soat-tech-challenge`) — faz deploy no EKS já existente, o CI/CD publica `/soat/producao/app/node-ip` (IP público de um node rodando o `soat-api`).
4. **lambda** (este repositório) — cria o segredo JWT (`/soat/producao/jwt/secret`, consumido pelo app na próxima sincronização de config) e o API Gateway, já apontando para o node do passo 3.

Depois do bootstrap inicial, cada repo aplica de forma independente — exceto que um deploy da app que troque o node ativo pode exigir reaplicar este repositório (ver nota de custo acima).

## API

- Rotas: `POST /auth/login-cpf` (público) e `ANY /api/{proxy+}` (protegido pelo Authorizer, encaminhado para a API principal).
- Collection Postman com exemplos de login por CPF: [collection.json](https://github.com/monnclaro/soat-tech-challenge/blob/main/collection.json) do repositório da aplicação (mesma collection, seção "Auth CPF").

## CI/CD

[.github/workflows/ci-cd.yml](.github/workflows/ci-cd.yml): build + testes .NET → `dotnet publish`/zip das duas funções → `terraform plan` (PR) ou `terraform apply` (push em `main`), reaproveitando o zip publicado como artifact do job de build. Segredos necessários: `AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY`, `AWS_SESSION_TOKEN` (do Academy, expiram com a sessão), `AWS_LAB_ROLE_ARN`.

## Links

- Diagrama de componentes completo, ADRs e RFCs: [soat-tech-challenge/docs](https://github.com/monnclaro/soat-tech-challenge/tree/main/docs)
