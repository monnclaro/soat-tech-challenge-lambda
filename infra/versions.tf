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
