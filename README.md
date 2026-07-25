# soat-tech-challenge-lambda

> Autenticação serverless por CPF e API Gateway da oficina — parte da Fase 3 do Tech Challenge FIAP: [app](https://github.com/monnclaro/soat-tech-challenge) · [infra-k8s](https://github.com/monnclaro/soat-tech-challenge-infra-k8s) · [infra-database](https://github.com/monnclaro/soat-tech-challenge-infra-database) · **lambda** (este repositório).

## Propósito

Uma AWS Lambda function + um API Gateway HTTP API que expõem uma segunda forma de login para proteger as rotas sensíveis do back-office da oficina:

- **AuthFunction** (`POST /auth/login-cpf`): valida o CPF informado, consulta existência/status (`Usuario.Ativo`) diretamente no RDS e devolve um JWT — a Function Serverless completa exigida pelo enunciado ("validar o CPF, consultar existência/status, gerar e devolver um token"), numa função só. Quem autentica aqui é `Usuario` (funcionário da oficina), não `Cliente`: o objetivo é proteger rotas sensíveis do back-office, e é o `Usuario` que precisa dessa segunda forma de login.

O token emitido aqui usa o **mesmo formato** aceito pelo `AddJwtAuthentication` do app principal (claims `Name`/`Role`, HS256) e a **mesma role** (`Admin`) — o login por CPF é uma segunda porta de entrada que coexiste com o login por email/senha já existente (`POST /api/auth/login`), não o substitui. A validação do token nas rotas protegidas acontece na própria API (já existente desde a Fase 1) — não há Lambda Authorizer neste repositório: seria uma segunda validação redundante do mesmo JWT, não exigida pelo enunciado.

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

Sem Secrets Manager (troca por SSM `SecureString`, grátis), sem IAM role própria (a função usa a `LabRole` já existente via `var.lab_role_arn` — o Academy bloqueia criação de roles), e a integração `/api/{proxy+}` aponta pro **IP público de um node do EKS + NodePort**, não pra um ALB (que custaria ~$16-20/mês). Trade-off: esse IP pode mudar se o node for substituído — o CI/CD do repo da app republica o parâmetro SSM a cada deploy, mas pode ser necessário um novo `terraform apply` deste repo depois de um deploy da app que trocou o node. Detalhes: ADR "Prioridade de custo e AWS Academy" (`soat-tech-challenge/docs/adr`).

## Arquitetura

```
Funcionário (via app/terminal da oficina)
     │
     ▼
┌─────────────────────────── API Gateway (HTTP API) ───────────────────────────┐
│                                                                                │
│  POST /auth/login-cpf                     ANY /api/{proxy+}                   │
│       │                                        │                              │
│       ▼                                        ▼ (sem authorizer no Gateway)  │
│  ┌───────────┐                                                                │
│  │AuthFunction│                          IP público do node EKS:NodePort      │
│  └─────┬─────┘                          → soat-api (valida o JWT sozinha)     │
│        │ valida CPF, consulta                                                 │
│        │ Usuario no RDS, gera JWT                                             │
│        ▼                                                                      │
│  SSM SecureString (jwt secret)                                                │
│  RDS (infra-database)                                                         │
└────────────────────────────────────────────────────────────────────────────┘
```

Diagrama de sequência completo do fluxo de autenticação: [soat-tech-challenge/docs/sequence-auth-cpf.md](https://github.com/monnclaro/soat-tech-challenge/blob/main/docs/sequence-auth-cpf.md).

## Estrutura

```
src/
├── Shared/              ← CpfValidator, JwtService, UsuarioRepository, LoginCpfService (lógica testável)
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

1. **infra-k8s** — cria VPC + EKS, publica `/soat/producao/network/*`.
2. **infra-database** — consome a VPC, cria o RDS, publica `/soat/producao/rds/*`.
3. **app** (`soat-tech-challenge`) — faz deploy no EKS já existente, o CI/CD publica `/soat/producao/app/node-ip` (IP público de um node rodando o `soat-api`).
4. **lambda** (este repositório) — cria o segredo JWT (`/soat/producao/jwt/secret`, consumido pelo app na próxima sincronização de config) e o API Gateway, já apontando para o node do passo 3.

Depois do bootstrap inicial, cada repo aplica de forma independente — exceto que um deploy da app que troque o node ativo pode exigir reaplicar este repositório (ver nota de custo acima).

## API

- Rotas: `POST /auth/login-cpf` (público) e `ANY /api/{proxy+}` (repassado direto pro node do EKS — a autorização é responsabilidade da própria API).
- Collection Postman com exemplos de login por CPF: [collection.json](https://github.com/monnclaro/soat-tech-challenge/blob/main/collection.json) do repositório da aplicação (mesma collection, seção "Auth CPF").

## CI/CD

[.github/workflows/ci-cd.yml](.github/workflows/ci-cd.yml): build + testes .NET → `dotnet publish`/zip da função → `terraform plan` (PR) ou `terraform apply` (push em `main`), reaproveitando o zip publicado como artifact do job de build. Segredos necessários: `AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY`, `AWS_SESSION_TOKEN` (do Academy, expiram com a sessão), `AWS_LAB_ROLE_ARN`.

## Links

- Diagrama de componentes completo, ADRs e RFCs: [soat-tech-challenge/docs](https://github.com/monnclaro/soat-tech-challenge/tree/main/docs)
