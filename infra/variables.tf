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

# ── Exposição da app (repo soat-tech-challenge, via NodePort — sem ALB) ────
variable "app_node_ip_ssm_parameter" {
  description = <<-EOT
    Parâmetro SSM publicado pelo CI/CD do repositório da app com o IP público
    de um node do EKS que está rodando o soat-api. Na primeira execução
    (bootstrap), esse parâmetro ainda não existe — ver ordem de deploy no
    README. Sem ALB (prioridade de custo — ver ADR "Prioridade de custo e
    AWS Academy"), então esse IP pode mudar se o node for substituído; o
    CI/CD da app republica o parâmetro a cada deploy.
  EOT
  type        = string
  default     = "/soat/producao/app/node-ip"
}

variable "app_node_port" {
  description = "NodePort do Service soat-api-service (k8s/service.yaml no repo da app)."
  type        = number
  default     = 30080
}
