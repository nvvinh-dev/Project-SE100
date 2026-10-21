# HouseholdFarm - Hệ thống quản lý nông nghiệp hộ gia đình

Ứng dụng web giúp hộ gia đình quản lý thửa đất, cây trồng, mùa vụ, công việc canh tác, kho vật tư, thu hoạch,
bán hàng, chi phí và báo cáo; có trợ lý AI (Google Gemini) làm việc trên dữ liệu đã ghi nhận của hộ.

- **Backend:** .NET 10, ASP.NET Core Web API, EF Core 10 + PostgreSQL 17.
- **Frontend:** React, TypeScript, Vite (đang dựng).

## Yêu cầu cài đặt

- .NET SDK 10
- Node.js 24 LTS
- Docker Desktop (Windows: bật WSL2)
- Git (Windows: kèm Git Bash để chạy script trong `scripts/`)

## Cấu trúc thư mục

```
├── docker-compose.yml      # PostgreSQL cho môi trường phát triển
├── .env.example            # mẫu file .env
├── .config/                # khóa phiên bản công cụ dotnet-ef
├── scripts/backup-db.sh    # sao lưu database demo
├── backend/
│   ├── HouseholdFarm.slnx
│   ├── src/HouseholdFarm.Api/
│   └── tests/HouseholdFarm.Api.Tests/
└── frontend/
```

## Chạy database bằng Docker

1. Chép `.env.example` thành `.env`, điền `POSTGRES_PASSWORD` (tự đặt, chỉ dùng trên máy mình). File `.env` không được commit.
2. Chạy ở thư mục gốc repo:

```bash
docker compose up -d
```

PostgreSQL 17 chạy ở `localhost:5432` (đổi bằng `POSTGRES_PORT` trong `.env`), database và user đều là `householdfarm`.

## Khai báo bí mật

Bí mật **không** ghi vào `appsettings.json` và không commit. Mỗi máy tự khai báo bằng `dotnet user-secrets`:

```bash
cd backend/src/HouseholdFarm.Api
dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=5432;Database=householdfarm;Username=householdfarm;Password=<mật khẩu trong .env>"
dotnet user-secrets set "Jwt:SigningKey" "<chuỗi ngẫu nhiên tối thiểu 32 ký tự>"
dotnet user-secrets set "Ai:ApiKey" "<khóa API Gemini>"
```

## Build, test, chạy backend

```bash
cd backend
dotnet tool restore
dotnet build
dotnet test
dotnet run --project src/HouseholdFarm.Api
```

- Backend chạy ở `http://localhost:5080`.
- Tài liệu API (chỉ ở môi trường Development): Swagger UI tại `http://localhost:5080/swagger`, tài liệu OpenAPI tại `/openapi/v1.json`.
- `dotnet test` cần Docker đang chạy, vì test tích hợp tự bật PostgreSQL tạm bằng Testcontainers.
- Log ghi ra console và thư mục `backend/src/HouseholdFarm.Api/logs/` (không commit).

## Sao lưu database demo

Database demo nằm trên Supabase (gói miễn phí không tự sao lưu). Sao lưu ít nhất mỗi tuần một lần và trước mỗi buổi demo:

```bash
export DB_URL='postgresql://<user>:<mật khẩu>@<host Session pooler>:5432/postgres'
bash scripts/backup-db.sh
```

File sao lưu nằm trong `backups/` (không commit). Cất thêm một bản ở nơi khác ngoài máy.
