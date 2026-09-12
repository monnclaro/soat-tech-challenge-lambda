# API Gateway HTTP API — roteia:
#   POST /auth/login-cpf         → Lambda AuthFunction (valida CPF, consulta
#                                   Cliente, emite JWT — a Function Serverless
#                                   completa exigida pelo enunciado, numa
#                                   função só)
#   ANY  /os/api/{proxy+}        → HTTP_PROXY para o NodePort do OS Service
#   ANY  /billing/api/{proxy+}   → HTTP_PROXY para o NodePort do Billing Service
#   ANY  /execucao/api/{proxy+}  → HTTP_PROXY para o NodePort do Execução Service
#
# Fase 4: 1 prefixo por microsserviço (em vez do único /api/{proxy+} da Fase
# 3, quando só existia 1 API) — cada prefixo é reposto pelo respectivo
# integration_uri como "/api/{proxy}", então o backend continua recebendo
# exatamente o path que já espera (ex.: "/api/v1/orcamentos"), sem precisar
# saber que está atrás de um prefixo no Gateway.
#
# Sem Lambda Authorizer: a validação do JWT nas rotas protegidas acontece na
# própria API (AddJwtAuthentication/[Authorize], já existente desde a Fase 1),
# não no Gateway — um authorizer aqui seria uma segunda validação redundante,
# não exigida pelo enunciado. Ver ADR "Sem Lambda Authorizer".
#
# O node só existe depois do primeiro deploy de algum dos 3 microsserviços
# (repos soat-tech-challenge-os-service/-billing-service/-execution-service).
# Na primeira aplicação deste repo, app_node_ip_ssm_parameter pode não existir
# ainda — ver ordem de deploy no README. Sem ALB, o IP de um node pode mudar
# se ele for substituído; o CI/CD de cada microsserviço republica o parâmetro
# a cada deploy, mas isso significa que este repo pode precisar de um novo
# apply depois de um deploy que trocou de node — trade-off aceito pela
# economia do ALB.

data "aws_ssm_parameter" "app_node_ip" {
  count = var.app_node_ip_ssm_parameter != "" ? 1 : 0
  name  = var.app_node_ip_ssm_parameter
}

locals {
  app_node_ip = length(data.aws_ssm_parameter.app_node_ip) > 0 ? data.aws_ssm_parameter.app_node_ip[0].value : null
}

resource "aws_apigatewayv2_api" "this" {
  name          = "soat-api-gateway-${var.environment}"
  protocol_type = "HTTP"
}

resource "aws_apigatewayv2_stage" "this" {
  api_id      = aws_apigatewayv2_api.this.id
  name        = "$default"
  auto_deploy = true
}

# ── Auth (público) ───────────────────────────────────────────────────────
resource "aws_apigatewayv2_integration" "auth" {
  api_id                 = aws_apigatewayv2_api.this.id
  integration_type       = "AWS_PROXY"
  integration_uri        = aws_lambda_function.auth.invoke_arn
  payload_format_version = "2.0"
}

resource "aws_apigatewayv2_route" "auth" {
  api_id    = aws_apigatewayv2_api.this.id
  route_key = "POST /auth/login-cpf"
  target    = "integrations/${aws_apigatewayv2_integration.auth.id}"
}

resource "aws_lambda_permission" "auth_invoke" {
  statement_id  = "AllowAPIGatewayInvokeAuth"
  action        = "lambda:InvokeFunction"
  function_name = aws_lambda_function.auth.function_name
  principal     = "apigateway.amazonaws.com"
  source_arn    = "${aws_apigatewayv2_api.this.execution_arn}/*/*"
}

# ── /os/api/* → NodePort do OS Service ───────────────────────────────────
# Sem authorizer no Gateway: passa direto, a API valida o Bearer JWT sozinha.
resource "aws_apigatewayv2_integration" "os_service" {
  count = local.app_node_ip != null ? 1 : 0

  api_id             = aws_apigatewayv2_api.this.id
  integration_type   = "HTTP_PROXY"
  integration_method = "ANY"
  # {proxy+} na rota captura só o que vem depois de /os/api/ (ex.:
  # "v1/ordens-servico") — o /api precisa ser reposto aqui, senão o
  # controller (que espera o path completo "api/v1/...") devolve 404.
  integration_uri = "http://${local.app_node_ip}:${var.os_service_node_port}/api/{proxy}"
}

resource "aws_apigatewayv2_route" "os_service" {
  count = local.app_node_ip != null ? 1 : 0

  api_id    = aws_apigatewayv2_api.this.id
  route_key = "ANY /os/api/{proxy+}"
  target    = "integrations/${aws_apigatewayv2_integration.os_service[0].id}"
}

# ── /billing/api/* → NodePort do Billing Service ─────────────────────────
resource "aws_apigatewayv2_integration" "billing_service" {
  count = local.app_node_ip != null ? 1 : 0

  api_id             = aws_apigatewayv2_api.this.id
  integration_type   = "HTTP_PROXY"
  integration_method = "ANY"
  integration_uri    = "http://${local.app_node_ip}:${var.billing_service_node_port}/api/{proxy}"
}

resource "aws_apigatewayv2_route" "billing_service" {
  count = local.app_node_ip != null ? 1 : 0

  api_id    = aws_apigatewayv2_api.this.id
  route_key = "ANY /billing/api/{proxy+}"
  target    = "integrations/${aws_apigatewayv2_integration.billing_service[0].id}"
}

# ── /execucao/api/* → NodePort do Execução Service ───────────────────────
resource "aws_apigatewayv2_integration" "execucao_service" {
  count = local.app_node_ip != null ? 1 : 0

  api_id             = aws_apigatewayv2_api.this.id
  integration_type   = "HTTP_PROXY"
  integration_method = "ANY"
  integration_uri    = "http://${local.app_node_ip}:${var.execucao_service_node_port}/api/{proxy}"
}

resource "aws_apigatewayv2_route" "execucao_service" {
  count = local.app_node_ip != null ? 1 : 0

  api_id    = aws_apigatewayv2_api.this.id
  route_key = "ANY /execucao/api/{proxy+}"
  target    = "integrations/${aws_apigatewayv2_integration.execucao_service[0].id}"
}
