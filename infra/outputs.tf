output "api_gateway_endpoint" {
  value = aws_apigatewayv2_stage.this.invoke_url
}

output "auth_function_name" {
  value = aws_lambda_function.auth.function_name
}

output "jwt_secret_ssm_parameter" {
  description = "Nome do parâmetro SSM SecureString com o segredo JWT (criado pelo infra-k8s, só consumido aqui)."
  value       = data.aws_ssm_parameter.jwt_secret.name
}
