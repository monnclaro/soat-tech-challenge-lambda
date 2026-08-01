terraform {
  required_version = ">= 1.6.0"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.60"
    }
    random = {
      source  = "hashicorp/random"
      version = "~> 3.6"
    }
  }

  # Backend remoto — state compartilhado entre execuções de CI/CD (a AWS
  # Academy reseta a conta entre sessões, então bucket/tabela não podem
  # depender de terem sido criados manualmente uma única vez). O workflow
  # (.github/workflows/ci-cd.yml) cria bucket e tabela se não existirem,
  # antes do terraform init — idempotente, roda em todo PR/push.
  backend "s3" {
    bucket         = "soat-tech-challenge-tfstate"
    key            = "lambda/terraform.tfstate"
    region         = "us-east-1"
    dynamodb_table = "soat-tech-challenge-tfstate-lock"
    encrypt        = true
  }
}

provider "aws" {
  region = var.aws_region

  default_tags {
    tags = {
      Projeto       = "soat-tech-challenge"
      Repositorio   = "soat-tech-challenge-lambda"
      GerenciadoPor = "terraform"
      Ambiente      = var.environment
    }
  }
}
