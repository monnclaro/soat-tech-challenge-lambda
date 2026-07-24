# Runtime "dotnet8": managed runtime da AWS para .NET, LTS. O app principal
# (soat-tech-challenge) roda em .NET 9, então este Lambda fica uma versão de
# .NET atrás dele de propósito — a AWS só publica managed runtime gerenciado
# para versões LTS do .NET, e .NET 9 (STS) não tem um "dotnet9" disponível
# (confirmado via `terraform validate`, que rejeita esse valor de runtime).
# Ver ADR "Lambda em .NET 8 (LTS), independente da versão do app".
#
# IAM restrito (AWS Academy): as duas funções usam a LabRole já existente
# (var.lab_role_arn) em vez de uma role criada por este Terraform. A LabRole
# do Academy já inclui as permissões equivalentes a
# AWSLambdaBasicExecutionRole + AWSLambdaVPCAccessExecutionRole.

resource "aws_lambda_function" "auth" {
  function_name = "soat-auth-${var.environment}"
  role          = var.lab_role_arn

  filename         = var.auth_function_zip
  source_code_hash = filebase64sha256(var.auth_function_zip)

  handler     = "AuthFunction::SoatTechChallenge.Lambda.Auth.Function::FunctionHandler"
  runtime     = "dotnet8"
  timeout     = 10
  memory_size = 512

  vpc_config {
    subnet_ids         = local.private_subnet_ids
    security_group_ids = [aws_security_group.lambda.id]
  }

  environment {
    variables = {
      DB_HOST     = data.aws_ssm_parameter.db_endpoint.value
      DB_PORT     = data.aws_ssm_parameter.db_port.value
      DB_NAME     = data.aws_ssm_parameter.db_name.value
      DB_USERNAME = data.aws_ssm_parameter.db_username.value
      DB_PASSWORD = data.aws_ssm_parameter.db_password.value
      JWT_SECRET  = aws_ssm_parameter.jwt_secret.value
    }
  }
}

resource "aws_lambda_function" "authorizer" {
  function_name = "soat-authorizer-${var.environment}"
  role          = var.lab_role_arn

  filename         = var.authorizer_function_zip
  source_code_hash = filebase64sha256(var.authorizer_function_zip)

  handler     = "AuthorizerFunction::SoatTechChallenge.Lambda.Authorizer.Function::FunctionHandler"
  runtime     = "dotnet8"
  timeout     = 5
  memory_size = 256

  # O Authorizer só valida a assinatura do JWT (CPU-bound); não precisa de
  # acesso à VPC/RDS, então fica fora da VPC — cold start mais rápido.

  environment {
    variables = {
      JWT_SECRET = aws_ssm_parameter.jwt_secret.value
    }
  }
}
