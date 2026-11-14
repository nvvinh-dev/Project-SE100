# HouseholdFarm - Hệ thống quản lý nông nghiệp hộ gia đình

Ứng dụng web giúp hộ gia đình quản lý thửa đất, cây trồng, mùa vụ, công việc canh tác, kho vật tư, thu hoạch,
bán hàng, chi phí và báo cáo; có trợ lý AI (Google Gemini) làm việc trên dữ liệu đã ghi nhận của hộ.

- **Backend:** .NET 10, ASP.NET Core Web API, EF Core 10 + PostgreSQL 17.
- **Frontend:** React 19, TypeScript 6, Vite 8, Tailwind CSS 4, shadcn/ui, TanStack Query.

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
    └── src/
        ├── app/            # router, query client, providers
        ├── components/ui/  # component shadcn/ui
        ├── lib/            # api client, enum, định dạng
        └── features/       # mỗi module một thư mục
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

## Database: migration và dữ liệu mẫu

Chạy ở thư mục `backend/`, sau khi database Docker đã chạy và đã khai báo bí mật:

```bash
dotnet tool restore
dotnet ef database update --project src/HouseholdFarm.Api
dotnet run --project src/HouseholdFarm.Api -- --seed
```

- `dotnet ef database update` đưa database lên migration mới nhất. Chạy lại mỗi khi pull code có migration mới.
- `-- --seed` tạo dữ liệu mẫu rồi thoát (không mở máy chủ). Lệnh **chỉ tạo khi database chưa có hộ nào**, chạy lại không nhân đôi dữ liệu.
- Muốn tạo lại dữ liệu mẫu từ đầu: xóa database rồi chạy lại hai lệnh trên.

```bash
dotnet ef database drop --project src/HouseholdFarm.Api
```

- Tạo migration mới (tên dạng `<Module>_<MoTa>`):

```bash
dotnet ef migrations add <Module>_<MoTa> --project src/HouseholdFarm.Api
```

### Tài khoản mẫu

Mọi tài khoản mẫu dùng chung mật khẩu `MatKhauMau@2026`. Mật khẩu này **chỉ dùng cho dữ liệu mẫu** trên máy phát triển và database demo;
không dùng cho tài khoản thật. Tên, số điện thoại đều là giả.

| Số điện thoại | Tài khoản | Hộ |
|---|---|---|
| `0900000001` | Quản trị hệ thống | (không thuộc hộ) |
| `0900000010` | Chủ hộ | Hộ mẫu A |
| `0900000011` | Thành viên hộ, đủ ba quyền bổ sung (kho, tài chính, chốt mùa vụ) | Hộ mẫu A |
| `0900000012` | Thành viên hộ, không có quyền bổ sung | Hộ mẫu A |
| `0900000013` | Nhân công | Hộ mẫu A |
| `0900000020` | Chủ hộ | Hộ mẫu B |
| `0900000021` | Thành viên hộ, đủ ba quyền bổ sung | Hộ mẫu B |
| `0900000022` | Thành viên hộ, không có quyền bổ sung | Hộ mẫu B |
| `0900000023` | Nhân công | Hộ mẫu B |

Hộ A có thửa `TD001`, `TD002` đang sử dụng và `TD003` ngừng sử dụng; hộ B có thửa `TD001`.

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

## Build, test, chạy frontend

```bash
cd frontend
npm ci
npm run dev
```

- Frontend chạy ở `http://localhost:5173`. Mọi yêu cầu `/api` được chuyển tiếp sang backend ở `http://localhost:5080`, nên chạy backend trước.
- Kiểm tra trước khi mở PR (CI chạy đúng các lệnh này):

```bash
npm run lint && npx tsc -b && npm test && npm run build
```

- Thêm component shadcn/ui: `npx shadcn add <tên component>` (dùng đúng bản CLI trong `package.json`).

## Sao lưu database demo

Database demo nằm trên Supabase (gói miễn phí không tự sao lưu). Sao lưu ít nhất mỗi tuần một lần và trước mỗi buổi demo:

```bash
export DB_URL='postgresql://<user>:<mật khẩu>@<host Session pooler>:5432/postgres'
bash scripts/backup-db.sh
```

File sao lưu nằm trong `backups/` (không commit). Cất thêm một bản ở nơi khác ngoài máy.
