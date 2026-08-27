# AI VND Billing Note

## Muc tieu

File nay ghi lai quyet dinh nghiep vu moi cua nhom cho phan thanh toan AI:

- Khong dung `credit` nua
- Dung vi `VND`
- So tien tru cua moi request AI duoc quy doi tu `reportedCostUsd`
- Co nhan them `ChargeMultiplier` do admin cau hinh
- Ty gia quy doi la `USD -> VND`
- Ap dung dung nghiep vu da mo ta trong `AI_VND_Billing_Business_Flow_For_BA.md`

---

## Nguyen tac nghiep vu da chot

### 1. Don vi thanh toan

- Don vi thanh toan phia user la `VND`
- User nap tien vao vi AI bang `VND`
- He thong khong dung `credit` va khong dung `token` lam so du vi phia user

### 2. Dieu kien toi thieu de duoc dung AI

- Neu `AI wallet balance < 1000 VND`:
  - Chan request ngay tu dau
  - Khong cho chay pipeline AI
  - Tra thong bao yeu cau nap them tien

- Neu `AI wallet balance >= 1000 VND`:
  - Cho phep request AI duoc chay

### 3. Cach tinh tien

Tien cua moi request AI duoc tinh tu `reportedCostUsd` ma provider tra ve.

Cong thuc:

```text
ChargedVnd = ceil(ReportedCostUsd * ChargeMultiplier * UsdToVndRate)
```

Trong do:

- `ReportedCostUsd`: chi phi USD ma provider tra ve cho request AI
- `ChargeMultiplier`: he so nhan gia do admin cau hinh
- `UsdToVndRate`: ty gia USD sang VND duoc cau hinh trong he thong
- `ceil(...)`: lam tron len de tranh tru thieu

Phan chi phi thuc te chua nhan he so van la:

```text
ActualCostVnd = ceil(ReportedCostUsd * UsdToVndRate)
```

Nghia la:

- `ActualCostVnd`: chi phi thuc te cua he thong theo ty gia
- `ChargedVnd`: so tien tinh cho user sau khi nhan them `ChargeMultiplier`

### 4. Truong hop vuot so du

Neu tai thoi diem bat dau request:

- `AI wallet balance >= 1000 VND`

thi he thong van cho request AI chay.

Sau khi request xong:

- Neu `ChargedVnd <= WalletBalance`: tru dung so tien
- Neu `ChargedVnd > WalletBalance`:
  - Van tra ket qua AI day du cho user
  - Chi tru vi ve `0`
  - Khong cho am vi
  - Phan chenh lech duoc ghi nhan la `AbsorbedVnd`

Cong thuc:

```text
ActualDeductedVnd = min(WalletBalance, ChargedVnd)
AbsorbedVnd = max(0, ChargedVnd - WalletBalance)
RemainingBalanceVnd = max(0, WalletBalance - ChargedVnd)
```

### 5. Khong dung credit nua

Sau khi chot nghiep vu nay:

- Khong dung `FreeCredit`
- Khong dung `PaidCredit`
- Khong dung `ChargedCredits`
- Khong dung `RefundCredits`

Neu trong code hien tai van con cac field tren, xem do la dau vet implementation cu, khong con la source-of-truth nghiep vu.

---

## Pham vi AI ap dung billing

Hien tai nghiep vu nay ap dung cho:

- BYOS Ingest
- Question Generation / Review
- AI Code Mentor

Co the mo rong sau cho cac chuc nang AI khac.

---

## Rule quan trong can chot trong code

### Rule 1: Chi charge theo `reportedCostUsd`

Billing chinh thuc cua user phai dua tren:

- `reportedCostUsd`

Khong dua tren:

- credit policy cu
- token policy cu

### Rule 2: Ty gia la cau hinh he thong

Can co cau hinh:

- `UsdToVndRate`
- `ChargeMultiplier`
- `MinBalanceVnd`

Vi du:

```text
UsdToVndRate = 26000
ChargeMultiplier = 1.5
MinBalanceVnd = 1000
```

Admin phai co man hinh rieng de cau hinh cac gia tri tren.

### Rule 3: Luu du trace de doi soat

Moi request AI thanh cong can luu it nhat:

- `ReportedCostUsd`
- `UsdToVndRate`
- `ChargeMultiplier`
- `ActualCostVnd`
- `ChargedVnd`
- `ActualDeductedVnd`
- `AbsorbedVnd`
- `RemainingBalanceVnd`
- `Provider`
- `Model`
- `UsageSource`
- `CostSource`

---

## De xuat du lieu nghiep vu moi

### User / Wallet

- `AiWalletBalanceVnd`

### AI usage / billing transaction

- `ReportedCostUsd`
- `UsdToVndRate`
- `ChargeMultiplier`
- `ActualCostVnd`
- `ChargedVnd`
- `ActualDeductedVnd`
- `AbsorbedVnd`
- `RemainingBalanceVnd`
- `Status`
- `FeatureKey`
- `SourceEntityType`
- `SourceEntityId`

---

## Vi du nghiep vu

### Vi du 1: Khong du dieu kien toi thieu

- Wallet = `800 VND`
- User bam AI Mentor

Ket qua:

- He thong chan ngay
- Khong chay AI
- Bao nap them tien

### Vi du 2: Tru binh thuong

- Wallet = `5000 VND`
- `ReportedCostUsd = 0.05`
- `UsdToVndRate = 26000`
- `ChargeMultiplier = 1.5`

Tinh:

```text
ActualCostVnd = ceil(0.05 * 26000) = 1300
ChargedVnd = ceil(0.05 * 1.5 * 26000) = 1950
```

Ket qua:

- Request thanh cong
- Tru `1950 VND`
- So du con `3050 VND`
- Chi phi thuc te cua he thong la `1300 VND`

### Vi du 3: Vuot so du nhung van cho ket qua

- Wallet = `1200 VND`
- `ReportedCostUsd = 0.06`
- `UsdToVndRate = 26000`
- `ChargeMultiplier = 1.5`

Tinh:

```text
ActualCostVnd = ceil(0.06 * 26000) = 1560
ChargedVnd = ceil(0.06 * 1.5 * 26000) = 2340
ActualDeductedVnd = 1200
AbsorbedVnd = 1140
RemainingBalanceVnd = 0
```

Ket qua:

- Request van thanh cong
- User van nhan ket qua AI
- Vi ve `0`
- He thong absorb `1140 VND`

---

## Ket luan

Source-of-truth nghiep vu moi cua nhom:

- User tra phi AI bang `VND`
- Dieu kien dung AI la `wallet >= 1000 VND`
- Chi phi thuc te cua he thong duoc quy doi tu `reportedCostUsd * ty gia USD/VND`
- Chi phi tinh cho user duoc tinh theo `reportedCostUsd * ChargeMultiplier * ty gia USD/VND`
- Neu request vuot so du thi van tra ket qua, chi tru ve `0`, phan con lai he thong absorb
- `credit` khong con duoc dung lam don vi billing phia user
