# Recursos criados:
#   - Segredo JWT como SSM SecureString (não Secrets Manager — custo, ver ADR
#     "Prioridade de custo e AWS Academy")
#   - Security Group do Lambda (só tráfego dentro da VPC — sem NAT, sem egress
#     pra internet, já que não há mais chamada a nenhuma API pública em runtime)
#   - As funções (usando a LabRole existente, sem criar IAM role) e o API
#     Gateway estão em lambda.tf / api-gateway.tf

data "aws_ssm_parameter" "vpc_id" {
  name = "/soat/${var.environment}/network/vpc-id"
}

data "aws_ssm_parameter" "private_subnet_ids" {
  name = "/soat/${var.environment}/network/private-subnet-ids"
}

data "aws_ssm_parameter" "vpc_cidr" {
  name = "/soat/${var.environment}/network/vpc-cidr"
}

data "aws_ssm_parameter" "db_endpoint" {
  name = "/soat/${var.environment}/rds/endpoint"
}

data "aws_ssm_parameter" "db_port" {
  name = "/soat/${var.environment}/rds/port"
}

data "aws_ssm_parameter" "db_name" {
  name = "/soat/${var.environment}/rds/db-name"
}

data "aws_ssm_parameter" "db_username" {
  name = "/soat/${var.environment}/rds/username"
}

data "aws_ssm_parameter" "db_password" {
  name            = "/soat/${var.environment}/rds/password"
  with_decryption = true
}

locals {
  vpc_id             = data.aws_ssm_parameter.vpc_id.value
  private_subnet_ids = split(",", data.aws_ssm_parameter.private_subnet_ids.value)
}

# ── Segredo JWT compartilhado ────────────────────────────────────────────
# Autoridade do segredo é este repositório (quem emite o token dos clientes).
# O app consome via /soat/{env}/jwt/secret — mesma convenção usada pelo
# infra-database para publicar credenciais do RDS.
resource "random_password" "jwt_secret" {
  length  = 48
  special = false # apenas alfanumérico: evita escaping ao injetar como env var
}

resource "aws_ssm_parameter" "jwt_secret" {
  name  = "/soat/${var.environment}/jwt/secret"
  type  = "SecureString"
  value = random_password.jwt_secret.result
}

# ── Rede ──────────────────────────────────────────────────────────────────
# Sem regra de egress pra internet: o Lambda só fala com o RDS (tráfego
# VPC-interno). Logs no CloudWatch continuam funcionando mesmo sem NAT — a
# entrega de logs do Lambda não passa pela ENI anexada à VPC.
resource "aws_security_group" "lambda" {
  name        = "soat-lambda-${var.environment}"
  description = "Egress do Lambda de autenticação restrito à VPC (acesso ao RDS)"
  vpc_id      = local.vpc_id

  egress {
    description = "Postgres (RDS) dentro da VPC"
    from_port   = 5432
    to_port     = 5432
    protocol    = "tcp"
    cidr_blocks = [data.aws_ssm_parameter.vpc_cidr.value]
  }
}
