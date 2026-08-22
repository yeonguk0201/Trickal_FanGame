UPDATE "run_items" AS run_item
SET "acquired_at" = run_record."started_at"
FROM "runs" AS run_record
WHERE run_item."run_id" = run_record."id"
  AND run_item."acquired_at" IS NULL;

ALTER TABLE "run_items"
ALTER COLUMN "acquired_at" SET NOT NULL;
