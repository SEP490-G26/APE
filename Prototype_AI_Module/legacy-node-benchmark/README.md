# Legacy NodeJS Benchmark

Thư mục này chứa benchmark NodeJS/React cũ.

## Trạng thái

- chỉ giữ lại để tham chiếu
- không còn là hướng phát triển chính
- không phải prototype được ưu tiên để merge vào BE thật

## Cấu trúc

- `backend/`: API benchmark cũ
- `frontend/`: UI benchmark cũ
- `package.json`: workspace scripts cho backend/frontend

## Chạy lại bản legacy

```bash
npm install
npm run dev:backend
npm run dev:frontend
```

## Ghi chú

Nếu cần benchmark hoặc phát triển tiếp, ưu tiên khối `.NET` ở root repo:

- `src-dotnet/`
- `Docs/`
