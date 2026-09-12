variable "aws_region" {
  type    = string
  default = "us-east-1"
}

variable "environment" {
  description = "Ambiente de deploy. Só existe 'producao' nesta fase — sem homologação, para minimizar custo (AWS Academy)."
  type        = string
  default     = "producao"

  validation {
    condition     = var.environment == "producao"
    error_message = "environment deve ser 'producao' — não há ambiente de homologação nesta fase."
  }
}

variable "lab_role_arn" {
  description = <<-EOT
    ARN da role IAM já existente na conta AWS Academy (normalmente "LabRole"),
    usada pela Lambda. O Academy bloqueia criação de roles/policies IAM
    novas, então não criamos nenhuma — só reaproveitamos esta.
  EOT
  type        = string
}

# ── Artefato publicado pelo CI/CD antes do terraform apply ─────────────────
variable "auth_function_zip" {
  description = "Caminho do zip publicado pelo `dotnet lambda package` do AuthFunction."
  type        = string
  default     = "../artifacts/auth-function.zip"
}

variable "jwt_expiration_hours" {
  type    = number
  default = 2
}

# ── Exposição dos microsserviços (Fase 4, via NodePort — sem ALB) ──────────
# Um único node group compartilhado pelos 3 microsserviços (mesmo cluster,
# repo infra-k8s) — o IP do node é uma propriedade do cluster, não de um
# serviço específico, então um único parâmetro SSM serve para os 3 (qualquer
# um dos 3 CI/CDs pode republicá-lo; NodePort roteia para o pod certo em
# qualquer node do cluster via kube-proxy).
variable "app_node_ip_ssm_parameter" {
  description = <<-EOT
    Parâmetro SSM publicado pelo CI/CD de qualquer um dos 3 repositórios de
    microsserviço com o IP público de um node do EKS. Na primeira execução
    (bootstrap), esse parâmetro ainda não existe — ver ordem de deploy no
    README. Sem ALB (prioridade de custo — ver ADR "Prioridade de custo e
    AWS Academy"), então esse IP pode mudar se o node for substituído; o
    CI/CD de cada microsserviço republica o parâmetro a cada deploy.
  EOT
  type        = string
  default     = "/soat/producao/app/node-ip"
}

variable "os_service_node_port" {
  description = "NodePort do Service soat-os-api-service (k8s/service.yaml no repo soat-tech-challenge-os-service)."
  type        = number
  default     = 30081
}

variable "billing_service_node_port" {
  description = "NodePort do Service soat-billing-api-service (k8s/service.yaml no repo soat-tech-challenge-billing-service)."
  type        = number
  default     = 30082
}

variable "execucao_service_node_port" {
  description = "NodePort do Service soat-execucao-api-service (k8s/service.yaml no repo soat-tech-challenge-execution-service)."
  type        = number
  default     = 30083
}
