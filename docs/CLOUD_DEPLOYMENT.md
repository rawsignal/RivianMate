# Cloud Deployment Guide

This guide covers deploying RivianMate as a cloud-hosted service (Pro Edition) on AWS or Azure. This is for operators who want to run RivianMate as a multi-tenant SaaS platform.

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                        Load Balancer                             │
│                    (ALB / Azure App Gateway)                     │
└─────────────────────────────────────────────────────────────────┘
                                │
                ┌───────────────┼───────────────┐
                ▼               ▼               ▼
        ┌───────────┐   ┌───────────┐   ┌───────────┐
        │  App (1)  │   │  App (2)  │   │  App (N)  │
        │  Container│   │  Container│   │  Container│
        └───────────┘   └───────────┘   └───────────┘
                │               │               │
                └───────────────┼───────────────┘
                                ▼
                    ┌───────────────────┐
                    │    PostgreSQL     │
                    │   (RDS / Azure)   │
                    └───────────────────┘
```

Key components:
- **Application containers**: Stateless, horizontally scalable
- **PostgreSQL**: Managed database (RDS or Azure Database)
- **Hangfire**: Background job processing (uses PostgreSQL for job queue)
- **Load balancer**: Distributes traffic, handles SSL termination

---

## AWS Deployment

### Option 1: AWS App Runner (Simplest)

App Runner is the simplest way to deploy containerized applications on AWS.

#### Prerequisites
- AWS account with appropriate permissions
- ECR repository for container images
- RDS PostgreSQL instance

#### Step 1: Create RDS PostgreSQL

```bash
# Using AWS CLI
aws rds create-db-instance \
  --db-instance-identifier rivianmate-prod \
  --db-instance-class db.t3.micro \
  --engine postgres \
  --engine-version 16 \
  --master-username rivianmate \
  --master-user-password <YOUR_SECURE_PASSWORD> \
  --allocated-storage 20 \
  --vpc-security-group-ids <YOUR_SG_ID> \
  --db-name rivianmate \
  --backup-retention-period 7 \
  --storage-encrypted
```

Or use the AWS Console:
1. RDS → Create database
2. PostgreSQL 16
3. Free tier or Production template
4. Configure credentials and storage
5. Enable encryption at rest

#### Step 2: Push Container to ECR

```bash
# Create ECR repository
aws ecr create-repository --repository-name rivianmate

# Authenticate Docker
aws ecr get-login-password --region us-east-1 | docker login --username AWS --password-stdin <ACCOUNT_ID>.dkr.ecr.us-east-1.amazonaws.com

# Build and push
docker build -t rivianmate .
docker tag rivianmate:latest <ACCOUNT_ID>.dkr.ecr.us-east-1.amazonaws.com/rivianmate:latest
docker push <ACCOUNT_ID>.dkr.ecr.us-east-1.amazonaws.com/rivianmate:latest
```

#### Step 3: Create App Runner Service

```bash
aws apprunner create-service \
  --service-name rivianmate \
  --source-configuration '{
    "ImageRepository": {
      "ImageIdentifier": "<ACCOUNT_ID>.dkr.ecr.us-east-1.amazonaws.com/rivianmate:latest",
      "ImageRepositoryType": "ECR",
      "ImageConfiguration": {
        "Port": "8080",
        "RuntimeEnvironmentVariables": {
          "DATABASE_URL": "Host=<RDS_ENDPOINT>;Database=rivianmate;Username=rivianmate;Password=<PASSWORD>",
          "ASPNETCORE_ENVIRONMENT": "Production",
          "RM_DK": "<your-deployment-key>",
          "Internal__DK": "<your-deployment-key>"
        }
      }
    },
    "AutoDeploymentsEnabled": true
  }' \
  --instance-configuration '{
    "Cpu": "1024",
    "Memory": "2048"
  }' \
  --health-check-configuration '{
    "Protocol": "HTTP",
    "Path": "/",
    "Interval": 10,
    "Timeout": 5,
    "HealthyThreshold": 1,
    "UnhealthyThreshold": 5
  }'
```

#### Step 4: Configure Custom Domain (Optional)

1. App Runner → Your service → Custom domains
2. Add your domain
3. Configure DNS CNAME to App Runner endpoint

---

### Option 2: ECS Fargate (Production)

For production workloads with more control over scaling and networking.

#### Terraform Configuration

```hcl
# main.tf

terraform {
  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.0"
    }
  }
}

provider "aws" {
  region = "us-east-1"
}

# VPC
module "vpc" {
  source  = "terraform-aws-modules/vpc/aws"
  version = "~> 5.0"

  name = "rivianmate-vpc"
  cidr = "10.0.0.0/16"

  azs             = ["us-east-1a", "us-east-1b"]
  private_subnets = ["10.0.1.0/24", "10.0.2.0/24"]
  public_subnets  = ["10.0.101.0/24", "10.0.102.0/24"]

  enable_nat_gateway = true
  single_nat_gateway = true
}

# RDS PostgreSQL
resource "aws_db_instance" "rivianmate" {
  identifier        = "rivianmate-prod"
  engine            = "postgres"
  engine_version    = "16"
  instance_class    = "db.t3.small"
  allocated_storage = 20

  db_name  = "rivianmate"
  username = "rivianmate"
  password = var.db_password

  vpc_security_group_ids = [aws_security_group.rds.id]
  db_subnet_group_name   = aws_db_subnet_group.rivianmate.name

  backup_retention_period = 7
  storage_encrypted       = true
  skip_final_snapshot     = false

  tags = {
    Environment = "production"
  }
}

# ECS Cluster
resource "aws_ecs_cluster" "rivianmate" {
  name = "rivianmate"

  setting {
    name  = "containerInsights"
    value = "enabled"
  }
}

# ECS Task Definition
resource "aws_ecs_task_definition" "rivianmate" {
  family                   = "rivianmate"
  network_mode             = "awsvpc"
  requires_compatibilities = ["FARGATE"]
  cpu                      = "512"
  memory                   = "1024"
  execution_role_arn       = aws_iam_role.ecs_execution.arn
  task_role_arn            = aws_iam_role.ecs_task.arn

  container_definitions = jsonencode([
    {
      name  = "rivianmate"
      image = "${aws_ecr_repository.rivianmate.repository_url}:latest"

      portMappings = [
        {
          containerPort = 8080
          protocol      = "tcp"
        }
      ]

      environment = [
        {
          name  = "ASPNETCORE_ENVIRONMENT"
          value = "Production"
        }
      ]

      secrets = [
        {
          name      = "DATABASE_URL"
          valueFrom = aws_secretsmanager_secret.db_connection.arn
        },
        {
          name      = "RM_DK"
          valueFrom = aws_secretsmanager_secret.deployment_key.arn
        },
        {
          name      = "Internal__DK"
          valueFrom = aws_secretsmanager_secret.deployment_key.arn
        }
      ]

      logConfiguration = {
        logDriver = "awslogs"
        options = {
          "awslogs-group"         = "/ecs/rivianmate"
          "awslogs-region"        = "us-east-1"
          "awslogs-stream-prefix" = "ecs"
        }
      }

      healthCheck = {
        command     = ["CMD-SHELL", "curl -f http://localhost:8080/ || exit 1"]
        interval    = 30
        timeout     = 5
        retries     = 3
        startPeriod = 60
      }
    }
  ])
}

# ECS Service
resource "aws_ecs_service" "rivianmate" {
  name            = "rivianmate"
  cluster         = aws_ecs_cluster.rivianmate.id
  task_definition = aws_ecs_task_definition.rivianmate.arn
  desired_count   = 2
  launch_type     = "FARGATE"

  network_configuration {
    subnets          = module.vpc.private_subnets
    security_groups  = [aws_security_group.ecs.id]
    assign_public_ip = false
  }

  load_balancer {
    target_group_arn = aws_lb_target_group.rivianmate.arn
    container_name   = "rivianmate"
    container_port   = 8080
  }

  depends_on = [aws_lb_listener.https]
}

# Application Load Balancer
resource "aws_lb" "rivianmate" {
  name               = "rivianmate-alb"
  internal           = false
  load_balancer_type = "application"
  security_groups    = [aws_security_group.alb.id]
  subnets            = module.vpc.public_subnets
}

resource "aws_lb_target_group" "rivianmate" {
  name        = "rivianmate-tg"
  port        = 8080
  protocol    = "HTTP"
  vpc_id      = module.vpc.vpc_id
  target_type = "ip"

  health_check {
    path                = "/"
    healthy_threshold   = 2
    unhealthy_threshold = 10
    timeout             = 60
    interval            = 300
    matcher             = "200"
  }

  stickiness {
    type            = "lb_cookie"
    cookie_duration = 86400
    enabled         = true
  }
}

resource "aws_lb_listener" "https" {
  load_balancer_arn = aws_lb.rivianmate.arn
  port              = "443"
  protocol          = "HTTPS"
  ssl_policy        = "ELBSecurityPolicy-TLS13-1-2-2021-06"
  certificate_arn   = var.certificate_arn

  default_action {
    type             = "forward"
    target_group_arn = aws_lb_target_group.rivianmate.arn
  }
}

# Auto Scaling
resource "aws_appautoscaling_target" "rivianmate" {
  max_capacity       = 10
  min_capacity       = 2
  resource_id        = "service/${aws_ecs_cluster.rivianmate.name}/${aws_ecs_service.rivianmate.name}"
  scalable_dimension = "ecs:service:DesiredCount"
  service_namespace  = "ecs"
}

resource "aws_appautoscaling_policy" "cpu" {
  name               = "rivianmate-cpu-scaling"
  policy_type        = "TargetTrackingScaling"
  resource_id        = aws_appautoscaling_target.rivianmate.resource_id
  scalable_dimension = aws_appautoscaling_target.rivianmate.scalable_dimension
  service_namespace  = aws_appautoscaling_target.rivianmate.service_namespace

  target_tracking_scaling_policy_configuration {
    predefined_metric_specification {
      predefined_metric_type = "ECSServiceAverageCPUUtilization"
    }
    target_value = 70
  }
}
```

#### Variables

```hcl
# variables.tf

variable "db_password" {
  description = "RDS master password"
  type        = string
  sensitive   = true
}

variable "certificate_arn" {
  description = "ACM certificate ARN for HTTPS"
  type        = string
}
```

#### Deploy

```bash
terraform init
terraform plan -var="db_password=<SECURE_PASSWORD>" -var="certificate_arn=<ACM_ARN>"
terraform apply
```

---

## Azure Deployment

### Option 1: Azure Container Apps (Simplest)

#### Prerequisites
- Azure subscription
- Azure CLI installed
- Container registry (ACR)

#### Step 1: Create Resources

```bash
# Create resource group
az group create --name rivianmate-rg --location eastus

# Create Azure Database for PostgreSQL
az postgres flexible-server create \
  --resource-group rivianmate-rg \
  --name rivianmate-db \
  --location eastus \
  --admin-user rivianmate \
  --admin-password <YOUR_SECURE_PASSWORD> \
  --sku-name Standard_B1ms \
  --tier Burstable \
  --storage-size 32 \
  --version 16

# Create database
az postgres flexible-server db create \
  --resource-group rivianmate-rg \
  --server-name rivianmate-db \
  --database-name rivianmate

# Create Container Registry
az acr create \
  --resource-group rivianmate-rg \
  --name rivianmateacr \
  --sku Basic \
  --admin-enabled true
```

#### Step 2: Push Container

```bash
# Login to ACR
az acr login --name rivianmateacr

# Build and push
az acr build --registry rivianmateacr --image rivianmate:latest .
```

#### Step 3: Create Container App

```bash
# Create Container Apps environment
az containerapp env create \
  --name rivianmate-env \
  --resource-group rivianmate-rg \
  --location eastus

# Get ACR credentials
ACR_PASSWORD=$(az acr credential show --name rivianmateacr --query "passwords[0].value" -o tsv)

# Create Container App
az containerapp create \
  --name rivianmate \
  --resource-group rivianmate-rg \
  --environment rivianmate-env \
  --image rivianmateacr.azurecr.io/rivianmate:latest \
  --registry-server rivianmateacr.azurecr.io \
  --registry-username rivianmateacr \
  --registry-password $ACR_PASSWORD \
  --target-port 8080 \
  --ingress external \
  --min-replicas 1 \
  --max-replicas 10 \
  --cpu 0.5 \
  --memory 1.0Gi \
  --env-vars \
    "DATABASE_URL=Host=rivianmate-db.postgres.database.azure.com;Database=rivianmate;Username=rivianmate;Password=<PASSWORD>;SSL Mode=Require" \
    "ASPNETCORE_ENVIRONMENT=Production" \
  --secrets \
    "rm-dk=<your-deployment-key>" \
    "internal-dk=<your-deployment-key>"
```

#### Step 4: Configure Custom Domain

```bash
az containerapp hostname add \
  --name rivianmate \
  --resource-group rivianmate-rg \
  --hostname app.rivianmate.com

# Add managed certificate
az containerapp hostname bind \
  --name rivianmate \
  --resource-group rivianmate-rg \
  --hostname app.rivianmate.com \
  --environment rivianmate-env \
  --validation-method CNAME
```

---

### Option 2: Azure Kubernetes Service (Production)

For production with full Kubernetes control.

#### Kubernetes Manifests

```yaml
# namespace.yaml
apiVersion: v1
kind: Namespace
metadata:
  name: rivianmate
---
# secret.yaml
apiVersion: v1
kind: Secret
metadata:
  name: rivianmate-secrets
  namespace: rivianmate
type: Opaque
stringData:
  DATABASE_URL: "Host=rivianmate-db.postgres.database.azure.com;Database=rivianmate;Username=rivianmate;Password=<PASSWORD>;SSL Mode=Require"
  DEPLOYMENT_KEY: "<your-deployment-key>"
---
# deployment.yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: rivianmate
  namespace: rivianmate
spec:
  replicas: 3
  selector:
    matchLabels:
      app: rivianmate
  template:
    metadata:
      labels:
        app: rivianmate
    spec:
      containers:
      - name: rivianmate
        image: rivianmateacr.azurecr.io/rivianmate:latest
        ports:
        - containerPort: 8080
        env:
        - name: ASPNETCORE_ENVIRONMENT
          value: "Production"
        - name: DATABASE_URL
          valueFrom:
            secretKeyRef:
              name: rivianmate-secrets
              key: DATABASE_URL
        - name: RM_DK
          valueFrom:
            secretKeyRef:
              name: rivianmate-secrets
              key: DEPLOYMENT_KEY
        - name: Internal__DK
          valueFrom:
            secretKeyRef:
              name: rivianmate-secrets
              key: DEPLOYMENT_KEY
        resources:
          requests:
            memory: "512Mi"
            cpu: "250m"
          limits:
            memory: "1Gi"
            cpu: "500m"
        livenessProbe:
          httpGet:
            path: /
            port: 8080
          initialDelaySeconds: 60
          periodSeconds: 30
        readinessProbe:
          httpGet:
            path: /
            port: 8080
          initialDelaySeconds: 30
          periodSeconds: 10
---
# service.yaml
apiVersion: v1
kind: Service
metadata:
  name: rivianmate
  namespace: rivianmate
spec:
  selector:
    app: rivianmate
  ports:
  - port: 80
    targetPort: 8080
  type: ClusterIP
---
# ingress.yaml
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: rivianmate
  namespace: rivianmate
  annotations:
    kubernetes.io/ingress.class: nginx
    cert-manager.io/cluster-issuer: letsencrypt-prod
spec:
  tls:
  - hosts:
    - app.rivianmate.com
    secretName: rivianmate-tls
  rules:
  - host: app.rivianmate.com
    http:
      paths:
      - path: /
        pathType: Prefix
        backend:
          service:
            name: rivianmate
            port:
              number: 80
---
# hpa.yaml
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: rivianmate
  namespace: rivianmate
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: rivianmate
  minReplicas: 2
  maxReplicas: 10
  metrics:
  - type: Resource
    resource:
      name: cpu
      target:
        type: Utilization
        averageUtilization: 70
```

---

## Environment Variables Reference

### Required for Cloud Deployment

| Variable | Description | Example |
|----------|-------------|---------|
| `DATABASE_URL` | PostgreSQL connection string | `Host=db.example.com;Database=rivianmate;Username=user;Password=pass` |
| `RM_DK` | Deployment key (see below) | `<your-secret-key>` |
| `ASPNETCORE_ENVIRONMENT` | Runtime environment | `Production` |

### Cloud Edition Activation

The cloud edition requires a deployment key. This prevents self-hosters from enabling cloud features.

**Setup:**

1. Generate a random secret key:
   ```bash
   openssl rand -base64 32
   ```

2. Add to your infrastructure secrets (AWS Secrets Manager, Azure Key Vault, etc.)

3. Set the environment variable in your container:
   ```yaml
   environment:
     - RM_DK=<your-generated-secret>
   ```

4. Add to your app configuration via User Secrets or secure config:
   ```bash
   # For local development/testing
   dotnet user-secrets set "Internal:DK" "<your-generated-secret>"

   # Or via environment variable in cloud
   Internal__DK=<your-generated-secret>
   ```

Both values must match for cloud edition to activate. Keep this key secure and never commit it to the repository.

### Optional Configuration

| Variable | Description | Default |
|----------|-------------|---------|
| `ASPNETCORE_URLS` | Listen URLs | `http://+:8080` |
| `Logging__LogLevel__Default` | Log level | `Information` |
| `RivianMate__Polling__IntervalAwakeSeconds` | Polling interval when vehicle awake | `30` |
| `RivianMate__Polling__IntervalAsleepSeconds` | Polling interval when vehicle asleep | `300` |

### Database Connection Options

You can provide the database connection in multiple ways:

1. **Full connection string:**
   ```
   DATABASE_URL=Host=hostname;Database=dbname;Username=user;Password=pass
   ```

2. **Individual components:**
   ```
   POSTGRES_HOST=hostname
   POSTGRES_PORT=5432
   POSTGRES_DB=rivianmate
   POSTGRES_USER=rivianmate
   POSTGRES_PASSWORD=secretpassword
   ```

3. **Azure with SSL:**
   ```
   DATABASE_URL=Host=server.postgres.database.azure.com;Database=rivianmate;Username=user;Password=pass;SSL Mode=Require
   ```

---

## Scaling Considerations

### Hangfire Background Jobs

RivianMate uses Hangfire for background polling jobs. Key considerations:

1. **Job storage**: Uses PostgreSQL (same database as application)
2. **Worker count**: Defaults to `Environment.ProcessorCount * 2`
3. **Queues**: `default` and `polling`

For high-scale deployments:
- Consider dedicated worker containers
- Tune worker count based on user count
- Monitor Hangfire dashboard at `/hangfire`

### Session Affinity

Blazor Server requires sticky sessions. Configure your load balancer:

- **AWS ALB**: Enable stickiness on target group
- **Azure**: Use cookie-based affinity
- **Kubernetes**: Use session affinity in Service or Ingress

### Database Scaling

For production workloads:

| Users | Recommended DB |
|-------|----------------|
| < 100 | db.t3.small / Standard_B1ms |
| 100-500 | db.t3.medium / Standard_B2s |
| 500-2000 | db.r6g.large / General Purpose 4 vCore |
| 2000+ | db.r6g.xlarge+ / Memory Optimized |

Enable read replicas for reporting/analytics queries if needed.

---

## Monitoring & Observability

### Health Checks

The application exposes a health endpoint at `/`:
- Returns 200 when healthy
- Used by load balancers and container orchestrators

### Logging

Structured logging via Serilog. Configure output:

```json
{
  "Serilog": {
    "WriteTo": [
      { "Name": "Console" },
      {
        "Name": "Seq",
        "Args": { "serverUrl": "http://seq:5341" }
      }
    ]
  }
}
```

### Metrics

Consider adding:
- Application Insights (Azure)
- CloudWatch (AWS)
- Prometheus + Grafana

### Alerts

Set up alerts for:
- High error rate (5xx responses)
- Database connection failures
- High memory/CPU usage
- Hangfire job failures

---

## CI/CD Pipeline

### GitHub Actions Example

```yaml
# .github/workflows/deploy.yml
name: Deploy to Production

on:
  push:
    branches: [main]

jobs:
  build-and-deploy:
    runs-on: ubuntu-latest

    steps:
    - uses: actions/checkout@v4

    - name: Configure AWS credentials
      uses: aws-actions/configure-aws-credentials@v4
      with:
        aws-access-key-id: ${{ secrets.AWS_ACCESS_KEY_ID }}
        aws-secret-access-key: ${{ secrets.AWS_SECRET_ACCESS_KEY }}
        aws-region: us-east-1

    - name: Login to ECR
      id: login-ecr
      uses: aws-actions/amazon-ecr-login@v2

    - name: Build and push
      env:
        ECR_REGISTRY: ${{ steps.login-ecr.outputs.registry }}
        IMAGE_TAG: ${{ github.sha }}
      run: |
        docker build -t $ECR_REGISTRY/rivianmate:$IMAGE_TAG .
        docker push $ECR_REGISTRY/rivianmate:$IMAGE_TAG
        docker tag $ECR_REGISTRY/rivianmate:$IMAGE_TAG $ECR_REGISTRY/rivianmate:latest
        docker push $ECR_REGISTRY/rivianmate:latest

    - name: Deploy to ECS
      run: |
        aws ecs update-service \
          --cluster rivianmate \
          --service rivianmate \
          --force-new-deployment
```

---

## Security Best Practices

1. **Secrets management**
   - Use AWS Secrets Manager or Azure Key Vault
   - Never commit secrets to source control
   - Rotate database passwords regularly

2. **Network security**
   - Database in private subnet only
   - Application behind load balancer
   - Use security groups/NSGs to limit access

3. **HTTPS only**
   - Redirect HTTP to HTTPS
   - Use TLS 1.2+ only
   - Regular certificate renewal (use ACM/managed certs)

4. **Data encryption**
   - Enable encryption at rest for RDS/Azure DB
   - Rivian tokens encrypted with Data Protection API

5. **Access control**
   - Hangfire dashboard restricted to admins
   - Implement proper authentication/authorization

---

## Cost Estimation

### AWS (Monthly)

| Component | Spec | Estimated Cost |
|-----------|------|----------------|
| ECS Fargate (2 tasks) | 0.5 vCPU, 1GB | ~$30 |
| RDS PostgreSQL | db.t3.small | ~$25 |
| ALB | Standard | ~$20 |
| Data transfer | 100GB | ~$10 |
| **Total** | | **~$85/month** |

### Azure (Monthly)

| Component | Spec | Estimated Cost |
|-----------|------|----------------|
| Container Apps | 0.5 vCPU, 1GB x 2 | ~$35 |
| PostgreSQL Flexible | Burstable B1ms | ~$25 |
| **Total** | | **~$60/month** |

*Costs vary by region and usage. Use AWS/Azure pricing calculators for accurate estimates.*
