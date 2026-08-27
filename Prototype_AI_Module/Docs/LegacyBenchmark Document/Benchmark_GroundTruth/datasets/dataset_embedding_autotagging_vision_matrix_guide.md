# Dataset Guide: Embedding + AutoTagging + Vision Matrix

## Muc tieu
- Co dinh:
`model_a = embed-multilingual-v3.0`
`model_b = command-r7b-12-2024`
- Xoay tua:
`model_c (vision) = gemini-2.5-flash, gemini-3.1-flash, gemini-3.5-flash, gemini-2.5-flash-lite, gemini-3.1-flash-lite`

## File dataset
- `dataset_embedding_autotagging_vision_matrix_15cases.csv`

## Nhom case
- `only_text` (5 cases): Vision phai = 0 (de check pipeline khong goi vision)
- `mixed_text_image` (5 cases): tai lieu text + image xen ke, so sanh cost vision theo model
- `vision_heavy_slide` (5 cases): tai lieu nhieu diagram/tree/array de stress test vision

## Cach chay de cong bang
1. Voi moi nhom, dung cung 1 input file goc cho ca 5 vision model.
2. Giu nguyen prompt vision + prompt autotagging.
3. Khong doi chunking strategy trong cung nhom case.
4. Ghi ket qua vao cac cot token/cost/latency.

## Luu y
- Case `EATG-VIS-001..005` da nap baseline tu so do that ban cung cap.
- Cac case `pending_run` de trong metric de ban dien sau khi chay.
