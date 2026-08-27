# Nghiệp Vụ Thanh Toán AI Bằng VND Cho BA

## 1. Mục đích tài liệu

Tài liệu này mô tả **nghiệp vụ thanh toán AI đã chốt ở mức business** để BA dùng làm đầu vào cho:

- context diagram
- swimlane
- use case specification
- use case diagram
- activity flow liên quan tới ví tiền và sử dụng chức năng AI

Tài liệu này ưu tiên mô tả **nghiệp vụ mục tiêu đã chốt của hệ thống**.  
Ở cuối tài liệu có phần ghi chú riêng về **trạng thái code hiện tại** để tránh nhầm giữa business target và implementation hiện tại.

---

## 2. Quyết định nghiệp vụ đã chốt

Hệ thống chốt mô hình thanh toán AI như sau:

- Đơn vị thanh toán phía người dùng là **VND**
- **Không dùng Credit**
- **Không dùng Token** làm đơn vị ví người dùng
- Người dùng nạp tiền vào tài khoản
- Khi sử dụng chức năng AI, hệ thống **trừ trực tiếp bằng VND** vào số dư tài khoản người dùng
- Nếu số dư tài khoản người dùng **nhỏ hơn 1000 VND**, hệ thống **không cho phép sử dụng bất kỳ chức năng AI nào**
- Nếu số dư tài khoản người dùng **từ 1000 VND trở lên**, người dùng được phép gửi request AI
- Hệ thống chấp nhận trường hợp request thực tế tốn nhiều hơn số dư còn lại của người dùng; khi đó hệ thống vẫn trả kết quả đầy đủ cho người dùng và chỉ trừ số dư về `0`
- Phần chênh lệch vượt quá số dư còn lại được xem là **chi phí hệ thống chấp nhận absorb**

---

## 3. Nguyên tắc nghiệp vụ cốt lõi

## 3.1. Đơn vị ví của người dùng

Ví AI của người dùng được quản lý bằng:

- `VND`

Không có các lớp trung gian như:

- credit
- internal token
- AI token làm số dư ví

AI token vẫn có thể được hệ thống dùng nội bộ để đo usage hoặc cost kỹ thuật, nhưng:

- **không hiển thị như đơn vị số dư ví**
- **không được xem là đơn vị thanh toán của người dùng**

## 3.2. Điều kiện tối thiểu để được dùng AI

Người dùng chỉ được phép sử dụng các chức năng AI nếu:

- `AI balance >= 1000 VND`

Nếu:

- `AI balance < 1000 VND`

thì hệ thống phải:

- chặn request AI ngay từ đầu
- không chạy pipeline AI
- trả thông báo yêu cầu nạp thêm tiền

## 3.3. Hệ thống chấp nhận absorb phần vượt

Nếu tại thời điểm bắt đầu request:

- người dùng có số dư `>= 1000 VND`

thì hệ thống cho phép request AI chạy.

Sau khi request hoàn tất:

- hệ thống tính số tiền phải trừ cho request đó
- nếu số tiền phải trừ nhỏ hơn hoặc bằng số dư hiện có thì trừ bình thường
- nếu số tiền phải trừ lớn hơn số dư hiện có thì hệ thống:
  - vẫn trả kết quả đầy đủ cho người dùng
  - chỉ trừ số dư người dùng về `0`
  - không để số dư âm
  - phần vượt được ghi nhận là phần hệ thống absorb

Ví dụ:

- người dùng còn `1200 VND`
- request AI thực tế bị tính `1800 VND`

Kết quả:

- request vẫn thành công
- người dùng nhận kết quả đầy đủ
- số dư ví còn `0`
- hệ thống absorb `600 VND`

## 3.4. Không cho phép số dư âm

Số dư ví AI của người dùng:

- không được âm

Nếu request lớn hơn số dư:

- chỉ trừ tối đa đến `0`

---

## 4. Các chức năng AI nằm trong phạm vi thanh toán

Các luồng AI hiện nằm trong phạm vi nghiệp vụ thanh toán:

- Student upload tài liệu BYOS để ingest AI
- Student tạo câu hỏi AI / generate + review
- Student dùng AI Code Mentor

Có thể mở rộng về sau cho các chức năng AI khác, nhưng ở giai đoạn hiện tại BA nên mô tả ít nhất ba luồng trên.

---

## 5. Tác nhân liên quan

## 5.1. Student

Student có thể:

- nạp tiền vào tài khoản
- xem số dư AI
- gửi request dùng chức năng AI
- nhận kết quả AI nếu đủ điều kiện tối thiểu
- nhận thông báo khi số dư nhỏ hơn 1000 VND

## 5.2. Hệ thống APE

Hệ thống có trách nhiệm:

- kiểm tra số dư trước khi chạy AI
- chặn request nếu số dư nhỏ hơn 1000 VND
- thực hiện pipeline AI nếu đủ điều kiện
- tính khoản tiền cần trừ cho request
- trừ số dư tối đa về 0
- ghi nhận phần absorb nếu request vượt số dư
- lưu usage log / billing log để thống kê

## 5.3. Cổng thanh toán / hệ thống nạp tiền

Tác nhân ngoài có trách nhiệm:

- xác nhận giao dịch nạp tiền thành công
- cập nhật số dư người dùng

## 5.4. Admin

Admin có thể:

- cấu hình hoặc quản trị chính sách giá AI
- xem thống kê usage, chi phí, số tiền đã trừ và phần hệ thống absorb

---

## 6. Đối tượng dữ liệu nghiệp vụ chính

BA có thể dùng các đối tượng dữ liệu nghiệp vụ sau trong tài liệu phân tích:

- `Student Account`
- `AI Wallet Balance (VND)`
- `AI Request`
- `AI Usage Log`
- `AI Billing Transaction`
- `AI Absorbed Loss`
- `Top-up Transaction`

---

## 7. Luồng nghiệp vụ tổng quát

## 7.1. Luồng nạp tiền

1. Student chọn nạp tiền
2. Student thanh toán qua cổng thanh toán
3. Cổng thanh toán xác nhận giao dịch thành công
4. Hệ thống cộng tiền vào số dư AI của Student
5. Student có thể tiếp tục dùng chức năng AI nếu số dư đạt điều kiện tối thiểu

## 7.2. Luồng dùng chức năng AI

1. Student gửi request dùng một chức năng AI
2. Hệ thống kiểm tra số dư AI hiện tại
3. Nếu số dư `< 1000 VND`:
   - từ chối request
   - trả thông báo yêu cầu nạp thêm tiền
4. Nếu số dư `>= 1000 VND`:
   - cho phép chạy pipeline AI
5. Hệ thống thực hiện xử lý AI
6. Hệ thống xác định số tiền cần trừ cho request
7. Hệ thống cập nhật ví:
   - nếu đủ tiền thì trừ đúng số tiền
   - nếu không đủ tiền thì trừ về `0`
8. Hệ thống ghi log usage và transaction
9. Nếu có phần vượt quá số dư ban đầu, hệ thống ghi nhận phần đó là absorbed loss
10. Trả kết quả AI cho Student

---

## 8. Luồng thay thế và ngoại lệ

## 8.1. Không đủ điều kiện tối thiểu để dùng AI

Điều kiện:

- số dư hiện tại `< 1000 VND`

Kết quả:

- request không được xử lý
- không gọi pipeline AI
- trả thông báo yêu cầu nạp thêm tiền

## 8.2. Request thành công nhưng vượt số dư

Điều kiện:

- số dư lúc bắt đầu `>= 1000 VND`
- request sau khi chạy xong có chi phí lớn hơn số dư còn lại

Kết quả:

- vẫn trả kết quả AI
- trừ ví về `0`
- không làm số dư âm
- phần chênh được ghi nhận là absorbed loss

## 8.3. Lỗi kỹ thuật trong quá trình AI chạy

Nếu request lỗi do kỹ thuật:

- không có kết quả AI hợp lệ
- BA có thể mô tả theo hướng:
  - request thất bại
  - hệ thống không ghi nhận kết quả hoàn chỉnh
  - hệ thống xử lý transaction theo rule kỹ thuật do backend quy định

Phần này BA không cần đi sâu vào cost engine, chỉ cần ghi:

- hệ thống có cơ chế ghi log và xử lý lỗi kỹ thuật riêng

---

## 9. Nghiệp vụ chi tiết theo từng chức năng

## 9.1. BYOS Ingest

### Mục tiêu

Student upload tài liệu cá nhân để hệ thống AI ingest thành dữ liệu có thể dùng cho retrieval và generation.

### Điều kiện trước

- Student đã đăng nhập
- Student có số dư AI `>= 1000 VND`

### Kết quả chính

- nếu đủ điều kiện tối thiểu: hệ thống cho phép chạy pipeline ingest
- nếu request ingest thành công: hệ thống trừ tiền vào ví
- nếu chi phí lớn hơn số dư hiện có: ví về `0`, phần chênh hệ thống absorb

### Điểm BA cần thể hiện trong swimlane

- Student gửi file
- Hệ thống kiểm tra min balance
- Hệ thống chạy AI ingest pipeline
- Hệ thống cập nhật số dư
- Hệ thống trả trạng thái ingest

## 9.2. Question Generation / Review

### Mục tiêu

Student yêu cầu hệ thống AI tạo câu hỏi dựa trên dữ liệu hệ thống hoặc BYOS.

### Điều kiện trước

- Student đã đăng nhập
- Student có số dư AI `>= 1000 VND`

### Kết quả chính

- nếu đủ điều kiện tối thiểu: hệ thống cho phép chạy generation + review
- nếu request thành công: hệ thống trừ tiền
- nếu request vượt số dư: trừ về `0`

## 9.3. AI Code Mentor

### Mục tiêu

Student gửi yêu cầu nhận phản hồi AI cho bài nộp PE.

### Điều kiện trước

- Student đã đăng nhập
- Student là chủ sở hữu submission
- Student có số dư AI `>= 1000 VND`

### Kết quả chính

- nếu đủ điều kiện tối thiểu: hệ thống chạy mentor
- nếu mentor thành công: hệ thống trừ tiền
- nếu chi phí vượt số dư: trừ về `0`

---

## 10. Gợi ý cho BA khi viết Context Diagram

BA có thể mô tả các thực thể mức cao như sau:

- `Student`
- `APE System`
- `Payment Gateway`
- `AI Provider`
- `Admin`

### Luồng chính

- Student gửi yêu cầu nạp tiền tới APE System
- APE System làm việc với Payment Gateway
- Payment Gateway trả kết quả nạp tiền
- Student gửi yêu cầu dùng AI tới APE System
- APE System gọi AI Provider để xử lý chức năng AI
- APE System cập nhật số dư và trả kết quả cho Student
- Admin xem log, transaction và thống kê

---

## 11. Gợi ý cho BA khi vẽ Swimlane

Các lane nên có:

- Student
- APE System
- Payment Gateway
- AI Provider
- Admin (nếu mô tả luồng quản trị / hậu kiểm)

### Swimlane cho luồng dùng AI nên có các bước chính

1. Student gửi request AI
2. APE System kiểm tra số dư
3. Nhánh điều kiện:
   - `< 1000 VND` -> từ chối
   - `>= 1000 VND` -> tiếp tục
4. APE System gọi AI Provider
5. AI Provider trả kết quả
6. APE System tính khoản trừ
7. APE System cập nhật ví
8. APE System ghi log / transaction / absorbed loss nếu có
9. APE System trả kết quả cho Student

---

## 12. Gợi ý cho BA khi viết Use Case

Các use case liên quan nên có:

- Nạp tiền vào ví AI
- Xem số dư ví AI
- Sử dụng AI BYOS Ingest
- Sử dụng AI Question Generation
- Sử dụng AI Code Mentor
- Xem lịch sử giao dịch AI
- Admin xem thống kê billing AI

### Điều kiện chung trước khi thực hiện các use case AI

- người dùng phải đăng nhập
- tài khoản còn tối thiểu `1000 VND`

### Điều kiện hậu use case AI

- số dư bị giảm tương ứng theo chi phí request
- hoặc về `0` nếu request vượt quá số dư còn lại
- usage log và billing log được ghi nhận

---

## 13. Business rules BA nên ghi rõ

BA nên viết rõ các business rule sau:

### BR-AI-PAY-01

Đơn vị số dư ví AI của người dùng là `VND`.

### BR-AI-PAY-02

Hệ thống không sử dụng `credit` hoặc `token` làm đơn vị số dư ví phía người dùng.

### BR-AI-PAY-03

Người dùng chỉ được phép sử dụng chức năng AI khi số dư ví AI hiện tại lớn hơn hoặc bằng `1000 VND`.

### BR-AI-PAY-04

Nếu số dư ví AI nhỏ hơn `1000 VND`, hệ thống phải từ chối request AI và yêu cầu người dùng nạp thêm tiền.

### BR-AI-PAY-05

Nếu request AI được phép chạy và chi phí thực tế lớn hơn số dư hiện có, hệ thống vẫn trả kết quả đầy đủ cho người dùng.

### BR-AI-PAY-06

Trong trường hợp request AI vượt số dư hiện có, hệ thống chỉ trừ số dư của người dùng về `0`, không làm số dư âm.

### BR-AI-PAY-07

Phần chi phí vượt quá số dư hiện có của người dùng được ghi nhận là phần hệ thống absorb.

### BR-AI-PAY-08

Mọi request AI được xử lý thành công phải có billing log / usage log phục vụ thống kê và quản trị.

---

## 14. Ví dụ nghiệp vụ để BA dễ diễn giải

## Ví dụ 1: Không đủ điều kiện tối thiểu

- Student còn `800 VND`
- Student bấm dùng AI Mentor

Kết quả:

- hệ thống chặn ngay
- không chạy AI
- hiển thị thông báo yêu cầu nạp thêm tiền

## Ví dụ 2: Được phép chạy và trừ bình thường

- Student còn `5000 VND`
- Student gửi request generate câu hỏi
- request đó bị tính `1800 VND`

Kết quả:

- hệ thống cho chạy
- trả kết quả đầy đủ
- số dư còn `3200 VND`

## Ví dụ 3: Được phép chạy nhưng vượt số dư

- Student còn `1200 VND`
- Student gửi request AI ingest tài liệu
- request đó thực tế bị tính `1900 VND`

Kết quả:

- hệ thống cho chạy vì tại đầu luồng user đủ `>= 1000 VND`
- hệ thống trả kết quả ingest đầy đủ
- ví người dùng bị trừ về `0`
- hệ thống absorb `700 VND`

---

## 15. Phạm vi BA nên và không nên đi sâu

## BA nên mô tả

- điều kiện dùng AI theo min balance
- luồng nạp tiền
- luồng trừ tiền khi dùng AI
- case vượt số dư nhưng vẫn trả kết quả
- actor, use case, activity flow, swimlane
- các business rule mức nghiệp vụ

## BA không cần đi quá sâu vào

- công thức token kỹ thuật nội bộ
- pricing engine chi tiết theo model/provider
- thuật toán estimate token
- cách backend tính chi phí raw từ provider

Các phần đó là chi tiết implementation của backend.

---

## 16. Lưu ý quan trọng về trạng thái code hiện tại

Để BA không nhầm giữa **nghiệp vụ đã chốt** và **code hiện tại**, cần lưu ý:

- Business target hiện đã chốt là **VND wallet**
- Tuy nhiên backend hiện tại vẫn còn dấu vết của mô hình `credit` trong một số entity, DTO và service runtime
- Vì vậy BA nên dùng tài liệu này như **source-of-truth cho nghiệp vụ cần đi tới**
- Khi viết SRS hoặc use case specification, BA nên mô tả theo:
  - ví AI bằng `VND`
  - `min balance = 1000 VND`
  - không dùng credit phía người dùng

---

## 17. Tóm tắt ngắn cho BA

Hệ thống AI của APE sử dụng ví AI theo đơn vị `VND`. Người dùng phải nạp tiền vào tài khoản trước khi sử dụng các chức năng AI. Hệ thống áp dụng điều kiện tối thiểu: nếu số dư ví AI nhỏ hơn `1000 VND` thì không cho phép gửi request AI. Nếu số dư từ `1000 VND` trở lên thì request được phép chạy. Sau khi AI xử lý xong, hệ thống trừ trực tiếp bằng `VND` vào số dư của người dùng. Nếu chi phí request lớn hơn số dư hiện có, hệ thống vẫn trả kết quả đầy đủ cho người dùng, chỉ trừ ví về `0` và ghi nhận phần chênh lệch là phần hệ thống absorb. Mô hình này giúp đơn giản hóa nghiệp vụ thanh toán AI, dễ mô tả cho tài liệu phân tích và phù hợp để BA xây dựng context diagram, swimlane, use case specification và use case diagram.
