# CONTRIBUTING

## Phạm vi sửa chính

- Mọi thay đổi đang active phải ưu tiên ở `src-dotnet/`.
- `legacy-node-benchmark/` chỉ giữ để tham chiếu benchmark cũ, không phải nơi phát triển chính.

## Khi nào sửa `src-dotnet/`

Sửa trong `src-dotnet/` khi thuộc một trong các nhóm sau:

- API, DTO, service, provider gateway
- UI operator prototype trong `Ape.AiModule.Api/wwwroot`
- prompt/policy/rubric/ground truth đang dùng bởi prototype `.NET`
- tài liệu của khối prototype trong `Docs/Prototype_Document/`

## Khi nào chỉ tham chiếu `legacy-node-benchmark/`

Chỉ đọc hoặc đối chiếu `legacy-node-benchmark/` khi cần:

- xem lại benchmark flow cũ
- so sánh cách test cũ với prototype mới
- lấy lại dữ liệu hoặc ý tưởng từ bản NodeJS trước đây

Không mở rộng tính năng mới ở đây nếu không có lý do rất rõ ràng.

## Quy ước tài liệu

- `Docs/LegacyBenchmark Document/`: tài liệu benchmark cũ
- `Docs/Prototype_Document/Guides/`: hướng dẫn vận hành, chạy, kiểm tra
- `Docs/Prototype_Document/Specs/`: đặc tả chức năng, pipeline, thiết kế
- `Docs/Prototype_Document/DB_Resources/`: mô tả collection và cấu trúc DB-ready

## Trước khi commit

- build lại `.NET` solution nếu có sửa code backend
- kiểm tra link tài liệu nếu có đổi tên file/thư mục trong `Docs/`
- không commit `.env`, `bin/`, `obj/`, `App_Data/run-history/`, `App_Data/uploads/`
