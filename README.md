# 🚀 ChatApp Backend - Enterprise Distributed Real-Time Architecture

![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?style=for-the-badge&logo=dotnet)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-16-4169E1?style=for-the-badge&logo=postgresql)
![SignalR](https://img.shields.io/badge/SignalR-WebSockets-red?style=for-the-badge)
![Redis](https://img.shields.io/badge/Redis-7-DC382D?style=for-the-badge&logo=redis)
![RabbitMQ](https://img.shields.io/badge/RabbitMQ-MassTransit-FF6600?style=for-the-badge&logo=rabbitmq)
![Kafka](https://img.shields.io/badge/Apache_Kafka-Event--Driven-231F20?style=for-the-badge&logo=apachekafka)
![Docker](https://img.shields.io/badge/Docker-Containerized-2496ED?style=for-the-badge&logo=docker)

Hệ thống Backend cho ứng dụng trò chuyện thời gian thực (**Real-time Multi-room Chat Application**), được xây dựng trên nền tảng **.NET 8 Web API** theo chuẩn kiến trúc **Clean Architecture (Onion Architecture)**. Dự án được thiết kế đáp ứng các tiêu chuẩn doanh nghiệp về hiệu năng cao (High Performance), khả năng mở rộng (Scalability), tính chịu lỗi (Fault Tolerance) và hỗ trợ sẵn sàng cho hạ tầng Cloud (AWS ECS/ECR/ALB).

---

## 💡 Điểm Nổi Bật Về Kiến Trúc & Kỹ Thuật (Engineering Highlights)

### 1. Clean Architecture (Onion Architecture)
Tách biệt hoàn toàn các tầng phụ thuộc theo nguyên lý **Dependency Inversion**:
- **Domain Layer**: Chứa Core Business Entities, Aggregates, Value Objects, Domain Events và Interfaces. Độc lập 100% với các thư viện bên ngoài.
- **Application Layer**: Chứa Use Cases, CQRS Handlers, DTOs, FluentValidation rules. Sử dụng **MediatR** để giảm coupling giữa API và Logic.
- **Infrastructure Layer**: Đảm nhận Persistence (EF Core, Dapper), Caching (Redis), Message Brokers (RabbitMQ, Kafka), SignalR Notifier.
- **API Layer**: RESTful Controllers, SignalR Hubs, Exception & Logging Middlewares.

### 2. CQRS Pattern & Hybrid Persistence (EF Core + Dapper)
- **Command (Ghi/Viết dữ liệu)**: Sử dụng **Entity Framework Core 8** với PostgreSQL. Tích hợp **Unit of Work** và **Repository Pattern** đảm bảo tính toàn vẹn dữ liệu (Atomicity) và quản lý transaction dễ dàng.
- **Query (Đọc dữ liệu)**: Sử dụng **Dapper** (NpgsqlDataSource) thực thi các câu lệnh SQL tối ưu trực tiếp cho truy vấn đọc history/online users, mang lại tốc độ phản hồi cực nhanh (Low-latency reads).
- **Snake Case Naming**: Tự động map thuộc tính PascalCase trong C# sang `snake_case` chuẩn PostgreSQL database.

### 3. Real-Time Communication (SignalR & WebSockets)
- Sử dụng **ASP.NET Core SignalR** kết hợp **Typed Hub Interface (`IChatClient`)** giúp đảm bảo type-safety compile-time.
- **Adapter Pattern (`SignalRChatNotifier`)**: Tách biệt logic phát notification của SignalR khỏi Application Layer. Application Layer chỉ gọi `IChatNotifier` abstraction.
- **Custom JSON Serialization**: Cấu hình tự động chuyển đổi camelCase (FE) <-> PascalCase (C# Models).
- Hỗ trợ **Redis Backplane**: Cho phép scale horizontal ra nhiều server instances mà vẫn duy trì sync WebSocket state giữa các clients.

### 4. Event-Driven & Async Messaging (Kafka & RabbitMQ)
- **Apache Kafka**: Tích hợp Producer & Background Consumer Service (`IHostedService`) cho việc phát và xử lý stream sự kiện hệ thống (audit log, analytics).
- **RabbitMQ & MassTransit**: Quản lý hàng đợi message không đồng bộ. Tích hợp:
  - **Exponential Backoff Retry Policy**: Tự động thử lại khi gặp sự cố mạng (retry 3 lần: 1s, 6s, 11s...).
  - **Fault Handling & Dead Letter Queue (DLQ)**: Xử lý lỗi tập trung thông qua `NewMessageFaultConsumer` giúp hệ thống không bị crash khi có payload lỗi.

### 5. Distributed Caching & Presence Tracking (Redis)
- Quản lý trạng thái Online/Offline của người dùng cực nhanh bằng **Redis 7**.
- Tích hợp **Redis Cache Service** cho danh sách phòng chat public và danh sách user online với TTL linh hoạt.

### 6. Resilience, Logging & Health Monitoring
- **Serilog Structured Logging**: Ghi log chuẩn định dạng JSON ra Console (development) và Rolling Daily Files (production), giúp dễ dàng phân tích qua ELK Stack hoặc Grafana Loki.
- **EF Core Resilience**: Tự động retry khi gặp sự cố transient kết nối PostgreSQL (`EnableRetryOnFailure`).
- **ASP.NET Core Health Checks**: Expose endpoint `/health` và `/health/ready` phục vụ Liveness & Readiness Probes trên Kubernetes / AWS ECS target groups.
- **Global Exception Middleware**: Bắt lỗi tập trung và trả về response định dạng chuẩn `ApiResponse<T>`.

---

## 🛠️ Công Nghệ & Thư Viện Sử Dụng (Tech Stack)

| Thành phần | Công nghệ / Thư viện |
| :--- | :--- |
| **Framework & Runtime** | .NET 8.0 (ASP.NET Core Web API) |
| **Architecture Patterns** | Clean Architecture, CQRS, Repository Pattern, Unit of Work, Adapter Pattern |
| **Database & ORM** | PostgreSQL 16, Entity Framework Core 8, Dapper, Npgsql |
| **Real-time Protocol** | SignalR WebSockets, Redis Backplane |
| **Caching** | Redis 7 (StackExchange.Redis) |
| **Message Brokers** | Apache Kafka, RabbitMQ (MassTransit) |
| **CQRS & Mapping** | MediatR 12, AutoMapper, FluentValidation |
| **Logging & Docs** | Serilog, Swagger / OpenAPI v1 |
| **Containerization** | Docker, Multi-stage Dockerfile |

---

## 📁 Cấu Trúc Dự Án (Project Structure)

```
ChatApp.Backend/
├── src/
│   ├── ChatApp.Domain/                # Core Layer: Entities, Aggregate Roots, Interfaces
│   │   ├── Entities/                  # User, Room, Message (Domain Encapsulation)
│   │   ├── Interfaces/                # Repository & UnitOfWork Contracts
│   │   └── ValueObjects/              # Domain Value Objects
│   │
│   ├── ChatApp.Application/           # Use Cases Layer: Business Logic
│   │   ├── Common/                    # Interfaces (IChatNotifier, ICacheService, IEventBus)
│   │   ├── DTOs/                      # Data Transfer Objects & Standard ApiResponse
│   │   └── Features/                  # CQRS Commands & Queries (Messages, Rooms, Users)
│   │
│   ├── ChatApp.Infrastructure/        # External Implementations Layer
│   │   ├── Caching/                   # RedisCacheService
│   │   ├── Messaging/                 # Kafka Producer/Consumer & RabbitMQ MassTransit Consumers
│   │   ├── Persistence/               # AppDbContext, Migrations, Repositories, UnitOfWork
│   │   └── DependencyInjection.cs     # Infrastructure DI Configuration
│   │
│   └── ChatApp.API/                   # Presentation Layer: Entry Point
│       ├── Controllers/               # REST API Endpoints (Messages, Users, Rooms)
│       ├── Hubs/                      # SignalR ChatHub & Payloads & IChatClient
│       ├── Middleware/                # Exception handling & Request logging
│       └── Program.cs                 # App Startup, Middleware Pipeline, DI Setup
│
├── Dockerfile.dev                     # Docker image configuration cho môi trường dev
└── .env.dev                           # Mẫu biến môi trường local
```

---

## 🔌 API Specification & SignalR Events

### 🌐 REST API Endpoints

| Method | Endpoint | Mô tả |
| :--- | :--- | :--- |
| `POST` | `/api/users` | Đăng ký hoặc đăng nhập User (Idempotent) |
| `GET` | `/api/users/online` | Lấy danh sách người dùng đang Online (Cache từ Redis) |
| `GET` | `/api/rooms` | Lấy danh sách các phòng chat Public |
| `GET` | `/api/messages?roomName={name}&limit={limit}&offset={offset}` | Truy vấn lịch sử tin nhắn của phòng chat |
| `GET` | `/health` | Health check endpoint kiểm tra kết nối Postgres & Redis |
| `GET` | `/swagger` | Giao diện Swagger UI tài liệu hóa API |

### ⚡ SignalR Hub (`ws://localhost:3000/hubs/chat`)

#### Client Invokes (Client -> Server):
- `JoinRoom({ username, avatarIndex, roomId })`: Tham gia vào phòng chat, tự động nhận 50 tin nhắn gần nhất.
- `SendMessage({ content, roomName })`: Gửi tin nhắn mới tới phòng chat.

#### Server Broadcasts (Server -> Client):
- `ReceiveMessage(MessageDto)`: Phát tin nhắn mới tới tất cả thành viên trong phòng.
- `ReceiveMessageHistory(IEnumerable<MessageDto>)`: Trả về lịch sử tin nhắn cho thành viên vừa tham gia.
- `UserJoined(UserDto)`: Thông báo có thành viên mới vào phòng.
- `UserLeft(username)`: Thông báo thành viên rời phòng/mất kết nối.
- `OnlineUsers(IEnumerable<UserDto>)`: Cập nhật danh sách người dùng online trong phòng.

---

## 🚀 Hướng Dẫn Cài Đặt & Chạy Dự Án (Getting Started)

### Yêu cầu tiên quyết (Prerequisites)
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Docker & Docker Desktop](https://www.docker.com/) (Dành cho việc chạy qua container)
- PostgreSQL 16, Redis 7 (Nếu chạy không qua Docker)

### 1. Chạy với Docker Compose (Khuyên dùng)
Tại thư mục gốc của repository lớn (`Deploy-Angular-Asp-Aws`):
```bash
docker compose -f docker-compose.dev.yaml up -d --build
```
Dịch vụ Backend sẽ sẵn sàng tại: `http://localhost:3000`  
Swagger UI: `http://localhost:3000/swagger`

### 2. Chạy Local qua Visual Studio / CLI
1. Di chuyển vào thư mục backend:
   ```bash
   cd ChatApp.Backend
   ```
2. Đảm bảo các dịch vụ PostgreSQL, Redis, RabbitMQ, Kafka đang hoạt động (có thể bật riêng qua Docker Compose).
3. Chạy ứng dụng:
   ```bash
   dotnet run --project src/ChatApp.API
   ```

---

## ☁️ Định Hướng Triển Khai Cloud (Cloud Deployment Strategy)

Dự án đã được thiết kế sẵn sàng cho việc đóng gói và triển khai trên hạ tầng **AWS**:
- **AWS ECR (Elastic Container Registry)**: Lưu trữ Docker Image được build từ `Dockerfile.dev` / `Dockerfile.prod`.
- **AWS ECS (Elastic Container Service)**: Chạy ứng dụng dạng microservice / task definitions.
- **AWS Application Load Balancer (ALB)**: Điều hướng HTTP request `/api/*` và kết nối WebSocket `/hubs/*` tới Backend Target Group (Port 8080).
- **IAM Roles & Security**: Sử dụng Task Roles kết nối bảo mật tới các AWS services (DynamoDB / ElastiCache) mà không hardcode Access Key.
