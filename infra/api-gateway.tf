# API Gateway HTTP API — roteia:
#   POST /auth/login-cpf  → Lambda AuthFunction (público, sem authorizer)
#   ANY  /api/{proxy+}    → HTTP_PROXY para o IP público de um node do EKS
#                            (Service NodePort, sem ALB — ver ADR "Prioridade
#                            de custo e AWS Academy"), protegido pelo Lambda
#                            Authorizer, que valida o JWT
#
# O node só existe depois do primeiro deploy do app (repo soat-tech-challenge).
# Na primeira aplicação deste repo, app_node_ip_ssm_parameter pode não existir
# ainda — ver ordem de deploy no README. Sem ALB, o IP de um node pode mudar
# se ele for substituído; o CI/CD da app republica o parâmetro a cada deploy,
# mas isso significa que este repo pode precisar de um novo apply depois de
# um deploy da app que trocou de node — trade-off aceito pela economia do ALB.

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

# ── Authorizer (Lambda REQUEST, simple response) ────────────────────────
resource "aws_apigatewayv2_authorizer" "jwt" {
  api_id                            = aws_apigatewayv2_api.this.id
  authorizer_type                   = "REQUEST"
  authorizer_uri                    = aws_lambda_function.authorizer.invoke_arn
  name                              = "soat-jwt-authorizer"
  authorizer_payload_format_version = "2.0"
  enable_simple_responses           = true
  identity_sources                  = ["$request.header.Authorization"]
  authorizer_result_ttl_in_seconds  = 30
}

resource "aws_lambda_permission" "authorizer_invoke" {
  statement_id  = "AllowAPIGatewayInvokeAuthorizer"
  action        = "lambda:InvokeFunction"
  function_name = aws_lambda_function.authorizer.function_name
  principal     = "apigateway.amazonaws.com"
  source_arn    = "${aws_apigatewayv2_api.this.execution_arn}/*/*"
}

# ── /api/* → node do EKS via NodePort (protegido pelo authorizer) ───────
resource "aws_apigatewayv2_integration" "app" {
  count = local.app_node_ip != null ? 1 : 0

  api_id             = aws_apigatewayv2_api.this.id
  integration_type   = "HTTP_PROXY"
  integration_method = "ANY"
  integration_uri    = "http://${local.app_node_ip}:${var.app_node_port}/{proxy}"
}

resource "aws_apigatewayv2_route" "app" {
  count = local.app_node_ip != null ? 1 : 0

  api_id             = aws_apigatewayv2_api.this.id
  route_key          = "ANY /api/{proxy+}"
  target             = "integrations/${aws_apigatewayv2_integration.app[0].id}"
  authorization_type = "CUSTOM"
  authorizer_id      = aws_apigatewayv2_authorizer.jwt.id
}
