# 📚 Cẩm Nang Sống Còn Khi Deploy ChatApp Lên Production

> **Tech Stack:** .NET 8 + Angular + Docker + AWS ECS/Fargate + ALB + Supabase PostgreSQL + SignalR + MassTransit + CloudAMQP + Redis

---

# 💀 Bảng Tử Thần: Những Pha Ăn Hành Nhớ Đời Khi Deploy Prod

## Kiếp nạn 1: Lệch Path ALB khiến Nginx trả HTML 503 thay vì WebSocket

### ❌ Lỗi gặp

Frontend gọi SignalR nhưng nhận về:

```text
503 Service Temporarily Unavailable
```

Response thực chất là một trang HTML từ **Nginx Frontend** thay vì response từ Backend.

### 🔍 Nguyên nhân

Frontend gọi endpoint:

```text
/hubs/chat/negotiate
```

Nhưng ALB không có rule định tuyến `/hubs/*` về Backend.

Request rơi vào Default Rule:

```text
Client
   ↓
ALB
   ↓
Default Action
   ↓
Frontend Target Group
   ↓
Nginx
   ↓
503 HTML
```

### ✅ Cách xử lý

Cấu hình ALB Listener Rules:

```text
/api/*
/hubs/*
/swagger/*
```

→ Route về **Backend Target Group**.

Các path còn lại:

```text
/*
```

→ Route về **Frontend Target Group**.

### 🧠 Bài học

> Mọi endpoint Backend đều phải được định tuyến rõ ràng, đặc biệt là SignalR Hub và WebSocket.

---

# Kiếp nạn 2: Vòng lặp Rollback của ECS Deployment Circuit Breaker

### ❌ Lỗi gặp

Task Definition Revision mới vừa deploy đã biến mất.

Sau đó một Revision cũ tưởng như đã chết lại xuất hiện:

```text
task-d-chatapp-backend-family:1
```

### 🔍 Nguyên nhân

Flow thực tế:

```text
Deploy Revision mới
        ↓
Container khởi động
        ↓
Health Check trả 503
        ↓
ALB đánh dấu Unhealthy
        ↓
ECS Deployment Circuit Breaker phát hiện deploy thất bại
        ↓
Rollback
        ↓
Revision cũ được chạy lại
```

ECS rollback dựa trên **deployment history**, không đơn giản là nhìn Task Definition đang Active hay Inactive.

### ⚠️ Điều dễ gây hiểu nhầm

Deregister Task Definition không có nghĩa ECS Service sẽ lập tức không thể rollback về Revision đó trong deployment context.

### ✅ Khi debug

Trong môi trường dev/staging có thể cân nhắc:

* Disable Deployment Circuit Breaker
* Disable Rollback
* Giảm Minimum Healthy Percent
* Tạm thời set Minimum Healthy Percent = `0%`

### 🧠 Bài học

> Nếu Revision mới chết vì Health Check, ECS có thể tự động rollback trước khi bạn kịp debug.

---

# Kiếp nạn 3: RabbitMQ làm `/health` chết theo

## ❌ Lỗi gặp

Log:

```text
Broker unreachable
```

Sau đó:

```text
Health check masstransit-bus with status Unhealthy
```

Cuối cùng:

```text
GET /health
503
```

Và:

```text
ALB
   ↓
503
   ↓
Target Unhealthy
   ↓
ECS terminate task
   ↓
Restart
   ↓
Lặp vô tận 🔄💀
```

## 🔍 Nguyên nhân

MassTransit tự đăng ký Health Check.

RabbitMQ không kết nối được:

```text
RabbitMQ ❌
    ↓
MassTransit Health Check ❌
    ↓
/health = 503
    ↓
ALB đánh giá Unhealthy
    ↓
ECS kill container
```

## ✅ Cách xử lý

Nên tách Health Check thành hai loại.

### Liveness

Container còn sống:

```text
/health/live
```

Chỉ kiểm tra application còn chạy.

### Readiness

Application đã sẵn sàng phục vụ:

```text
/health/ready
```

Kiểm tra các dependency cần thiết.

Ví dụ:

```text
ALB
 ↓
/health/live
 ↓
200 OK
```

Không nên để một dependency phụ như RabbitMQ làm chết toàn bộ Backend nếu hệ thống vẫn có thể hoạt động một phần.

### 🧠 Bài học

> Health Check cho ALB không nhất thiết phải kiểm tra toàn bộ thế giới bên ngoài.

---

# Kiếp nạn 4: RabbitMQ `ACCESS_REFUSED` do Port và SSL

## ❌ Lỗi gặp

```text
Broker unreachable
```

Hoặc:

```text
ACCESS_REFUSED
```

## 🔍 Nguyên nhân

RabbitMQ có hai kiểu kết nối phổ biến:

| Port   | Protocol | SSL       |
| ------ | -------- | --------- |
| `5672` | AMQP     | Không SSL |
| `5671` | AMQPS    | SSL/TLS   |

Nếu kết nối:

```text
Port 5671
```

nhưng không cấu hình SSL phù hợp, RabbitMQ có thể từ chối kết nối.

## ⚠️ Bẫy cấu hình

Nếu application không đọc đúng biến:

```text
RabbitMQ__Port
```

thì có thể rơi vào port mặc định khác với mong muốn.

## ✅ Cách xử lý

Ví dụ Environment Variables:

```text
RabbitMQ__Host=leopard.lmq.cloudamqp.com
RabbitMQ__Port=5672
RabbitMQ__Username=<username>
RabbitMQ__Password=<password>
RabbitMQ__VirtualHost=<vhost>
```

Nếu sử dụng port SSL:

```text
5671
```

thì phải cấu hình SSL/TLS tương ứng.

### 🧠 Bài học

> Port đúng chưa đủ. Protocol và SSL configuration cũng phải đúng.

---

# Kiếp nạn 5: Target Group không cho Success Code 5xx

## ❌ Lỗi gặp

Muốn cấu hình:

```text
200,503
```

nhưng AWS báo lỗi:

```text
HTTP code must be between 200 and 499
```

## 🔍 Nguyên nhân

Target Group Health Check không cho phép xem `5xx` là thành công.

Điều này hợp lý vì:

```text
5xx = Server Error
```

## ⚠️ Cách debug tạm thời

Có thể:

* Dùng endpoint riêng trả `200`
* Hoặc tạm thời dùng một route có `404` và cho phép Health Check matcher nhận `404`

Ví dụ:

```text
Health Check Path:
/healthz
```

Matcher:

```text
404
```

### ⚠️ Lưu ý

Đây chỉ nên là cách **debug tạm thời**.

Production nên có endpoint Health Check đúng nghĩa:

```text
/health/live
```

trả:

```text
200
```

### 🧠 Bài học

> Đừng cố ép AWS coi 503 là khỏe. Hãy tạo Health Check endpoint phù hợp.

---

# Kiếp nạn 6: PostgreSQL Not-Null Constraint khi Join Room

## ❌ Lỗi gặp

```text
23502: null value in column "last_seen_at"
```

## 🔍 Nguyên nhân

C# Entity:

```csharp
public DateTime? LastSeenAt { get; set; }
```

Hoặc chưa gán giá trị:

```csharp
LastSeenAt = null;
```

Nhưng PostgreSQL:

```sql
last_seen_at TIMESTAMPTZ NOT NULL
```

Khi gọi:

```text
JoinRoom
```

EF Core insert User và bị PostgreSQL chặn.

## ✅ Cách xử lý

### Cách 1: Gán giá trị trong code

```csharp
LastSeenAt = DateTime.UtcNow;
```

### Cách 2: Cho phép null

```sql
ALTER TABLE users
ALTER COLUMN last_seen_at DROP NOT NULL;
```

### Cách 3: Default ở Database

```sql
last_seen_at TIMESTAMPTZ NOT NULL DEFAULT NOW();
```

### 🧠 Bài học

> Nullable trong C# và NULL/NOT NULL trong PostgreSQL phải đồng bộ.

---

# Kiếp nạn 7: BaseEntity Soft Delete nhưng Database thiếu cột

## ❌ Lỗi gặp

```text
42703: column "is_deleted" does not exist
```

## 🔍 Nguyên nhân

Entity:

```text
Message
    ↓
BaseEntity
    ↓
IsDeleted
    ↓
DeletedAt
```

EF Core generate SQL:

```sql
INSERT INTO messages (
    ...
    is_deleted
)
VALUES (...);
```

Nhưng Database được tạo bằng SQL script thủ công và không có:

```text
is_deleted
```

## ✅ Cách xử lý

Các bảng kế thừa `BaseEntity` cần có:

```sql
is_deleted BOOLEAN NOT NULL DEFAULT false,
deleted_at TIMESTAMPTZ NULL
```

### 🧠 Bài học

> Nếu dùng EF Core Entity inheritance mà tự viết SQL schema, phải map đủ toàn bộ cột từ Base Class.

---

# Kiếp nạn 8: CORS thiếu Protocol

## ❌ Lỗi gặp

Environment Variable:

```text
AllowedOrigins__0=alb-chatapp-xxxx.elb.amazonaws.com
```

Nhưng Browser gửi:

```text
Origin: http://alb-chatapp-xxxx.elb.amazonaws.com
```

Backend so sánh strict equality:

```text
Domain ❌ Origin
```

## ✅ Cách xử lý

Phải có protocol:

```text
AllowedOrigins__0=http://alb-chatapp-xxxx.elb.amazonaws.com
```

Nếu dùng HTTPS:

```text
AllowedOrigins__0=https://example.com
```

### 🧠 Bài học

> CORS Origin không chỉ là domain. Nó bao gồm scheme + host + port.

---

# 🐰 CloudAMQP và MassTransit Consumer

## CloudAMQP Free Plan

Các Free Plan có giới hạn quota theo từng thời điểm.

Những thứ cần đặc biệt theo dõi:

* Số lượng messages
* Số connection
* RAM
* Connection lifetime

## 💀 Nguy hiểm: Consumer lỗi + Retry vô hạn

Ví dụ:

```text
Message
   ↓
Consumer
   ↓
Exception
   ↓
Requeue
   ↓
Consumer nhận lại
   ↓
Exception
   ↓
Requeue
   ↓
Lặp vô tận 🔄
```

Hậu quả:

```text
1 message
   ↓
100 retries
   ↓
1,000 retries
   ↓
10,000 retries
```

Quota message bay màu rất nhanh.

## ✅ Nên giới hạn Retry

Ví dụ:

```csharp
cfg.UseMessageRetry(r =>
{
    r.Interval(3, TimeSpan.FromSeconds(5));
});
```

Sau số lần retry giới hạn, message nên được chuyển sang:

```text
Error Queue
```

Hoặc:

```text
Dead Letter Queue
```

### 🧠 Bài học

> Retry vô hạn không phải Reliability. Đó là một chiếc máy xay quota.

---

# 🐘 Supabase PostgreSQL

## Connection String

Supabase Dashboard:

```text
Project
→ Connect
→ Connection String
```

Ví dụ:

```text
Host=aws-0-xxx.pooler.supabase.com;
Port=5432;
Database=postgres;
Username=postgres.<project-ref>;
Password=<password>;
SSL Mode=Require;
Trust Server Certificate=true
```

## Các loại Port phổ biến

| Connection         |                                    Port |
| ------------------ | --------------------------------------: |
| Direct Connection  |                                  `5432` |
| Session Pooler     | Có thể dùng port được Supabase cung cấp |
| Transaction Pooler |                                  `6543` |

> Luôn lấy chính xác connection string từ Supabase Dashboard thay vì tự đoán port.

## ⚠️ RLS

Nếu Backend kết nối trực tiếp bằng PostgreSQL credentials, cần hiểu rõ quyền database và RLS trước khi bật policy.

Không nên mặc định suy luận rằng chỉ cần bật hoặc tắt RLS là mọi backend connection sẽ tự hoạt động.

### 🧠 Bài học

> Database schema, DB credentials và RLS policy phải được thiết kế đồng bộ.

---

# 🔴 Upstash Redis

Dùng cho:

* SignalR Backplane
* Distributed Cache
* Multi-instance Backend

## Connection String

Ví dụ:

```text
<endpoint>.upstash.io:6379,
password=<token>,
ssl=True,
abortConnect=False
```

## Lưu ý

### `ssl=True`

Kết nối Redis qua Internet cần TLS theo endpoint được provider cung cấp.

### `abortConnect=False`

Giúp application không chết ngay khi Redis chưa kết nối được tại thời điểm startup.

```text
App Start
   ↓
Redis tạm thời unavailable
   ↓
App vẫn chạy
   ↓
Redis reconnect sau
```

---

# 🟠 Confluent Cloud Kafka

## Cấu hình cơ bản

```text
Kafka__BootstrapServers=<server>:9092
Kafka__SaslMechanism=Plain
Kafka__SecurityProtocol=SaslSsl
Kafka__SaslUsername=<api-key>
Kafka__SaslPassword=<api-secret>
```

## ⚠️ Bài học

Kafka Cloud không đơn giản là:

```text
host:port
```

Còn phải có:

```text
SASL
+
SSL
+
API Key
+
API Secret
```

Sai một mảnh là connection fail.

---

# 🌐 Kiến Trúc Port & Routing

## Backend

```text
Container Port: 8080
```

ASP.NET Core:

```text
ASPNETCORE_URLS=http://+:8080
```

Flow:

```text
Internet
   ↓
ALB :80
   ↓
Target Group
   ↓
Backend Container :8080
```

---

## Frontend

```text
Container Port: 80
```

Nginx serve Angular static files.

Flow:

```text
Internet
   ↓
ALB :80
   ↓
Frontend Target Group
   ↓
Nginx :80
```

---

# 🗺️ ALB Routing Rules

## Backend

```text
/api/*
/hubs/*
/swagger/*
```

↓

```text
Backend Target Group
```

## Frontend

Default Action:

```text
/*
```

↓

```text
Frontend Target Group
```

## Tổng quan

```text
                    Internet
                        │
                        ▼
                      ALB
                        │
          ┌─────────────┴─────────────┐
          │                           │
          ▼                           ▼
   /api/* /hubs/*               Other Paths
   /swagger/*                         │
          │                           │
          ▼                           ▼
   Backend Target Group       Frontend Target Group
          │                           │
          ▼                           ▼
     .NET :8080                   Nginx :80
```

---

# 💰 Quy Tắc Tắt/Bật Hệ Thống

## Muốn tiết kiệm Fargate

Không nhất thiết phải xóa toàn bộ infrastructure.

Có thể:

```text
ECS Service
Desired Count = 0
```

Khi đó:

```text
ECS Task = 0
```

→ Không còn Fargate compute cho các task đó.

## ⚠️ Nhưng lưu ý

Các resource khác vẫn có thể tiếp tục phát sinh chi phí:

* Application Load Balancer
* NAT Gateway
* Database
* Redis
* Public IPv4
* Data Transfer

### ❌ Không nên xóa ALB nếu chỉ muốn tạm dừng project

Xóa ALB có thể dẫn đến:

```text
ALB DNS thay đổi
    ↓
Frontend config phải sửa
    ↓
Backend CORS phải sửa
    ↓
Deploy lại
```

### 🧠 Bài học

> Tắt ECS Task giúp giảm compute cost, nhưng không có nghĩa toàn bộ AWS infrastructure đã miễn phí.

---

# 🚨 Checklist Trước Khi Deploy Production

## Backend

* [ ] `ASPNETCORE_URLS` đúng port
* [ ] Connection String PostgreSQL hoạt động
* [ ] Redis kết nối được
* [ ] RabbitMQ kết nối được
* [ ] Kafka kết nối được nếu có sử dụng
* [ ] Health Check trả `200`
* [ ] CORS có đầy đủ protocol
* [ ] Environment Variables đúng tên
* [ ] Không hardcode secrets

## Database

* [ ] Schema khớp Entity
* [ ] Nullable khớp với `NULL/NOT NULL`
* [ ] BaseEntity có đủ các cột
* [ ] Default values hoạt động
* [ ] Migration hoặc SQL script được kiểm tra

## AWS ECS

* [ ] Task Definition dùng đúng Image
* [ ] Container Port đúng
* [ ] Target Group Port đúng
* [ ] Security Group mở đúng traffic
* [ ] Health Check Path đúng
* [ ] Success Code hợp lệ
* [ ] Circuit Breaker được kiểm tra
* [ ] Logs CloudWatch hoạt động

## ALB

* [ ] `/api/*` → Backend
* [ ] `/hubs/*` → Backend
* [ ] `/swagger/*` → Backend
* [ ] Default → Frontend
* [ ] Target Group Healthy

## SignalR

* [ ] `/hubs/*` được route đúng
* [ ] WebSocket được ALB hỗ trợ
* [ ] CORS đúng Origin
* [ ] Redis Backplane hoạt động nếu chạy nhiều Backend instances

## RabbitMQ

* [ ] Host đúng
* [ ] Port đúng
* [ ] SSL configuration đúng
* [ ] Username đúng
* [ ] Password đúng
* [ ] VirtualHost đúng
* [ ] Retry có giới hạn
* [ ] Error Queue được theo dõi

---

# 🧠 Kết Luận

Khi deploy một hệ thống:

```text
Angular
   ↓
ALB
   ↓
ECS
   ↓
.NET API
   ├── PostgreSQL
   ├── Redis
   ├── RabbitMQ
   └── Kafka
```

Một lỗi nhỏ trong:

```text
Port
Path
Health Check
SSL
Environment Variable
Database Schema
CORS
```

cũng có thể tạo ra hiệu ứng domino:

```text
Một config sai
    ↓
Service lỗi
    ↓
Health Check 503
    ↓
ALB Unhealthy
    ↓
ECS kill task
    ↓
Circuit Breaker rollback
    ↓
Deploy loop 💀
```

## Nguyên tắc sống còn

> **Tách Health Check, kiểm tra đúng Routing, đồng bộ Schema, và đừng tin rằng service đã chạy nghĩa là toàn hệ thống đã hoạt động.**
