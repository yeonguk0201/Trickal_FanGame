-- AlterTable
ALTER TABLE "users" ADD COLUMN     "elif_balance" INTEGER NOT NULL DEFAULT 0;

-- AlterTable
ALTER TABLE "runs" ADD COLUMN     "elif_gained" INTEGER NOT NULL DEFAULT 0,
ADD COLUMN     "elif_shortfall" INTEGER NOT NULL DEFAULT 0,
ADD COLUMN     "elif_snapshot" JSONB,
ADD COLUMN     "elif_spent" INTEGER NOT NULL DEFAULT 0;

-- CreateTable
CREATE TABLE "user_shop_upgrades" (
    "id" UUID NOT NULL,
    "user_id" UUID NOT NULL,
    "upgrade_id" VARCHAR(64) NOT NULL,
    "level" INTEGER NOT NULL,
    "created_at" TIMESTAMPTZ(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "updated_at" TIMESTAMPTZ(3) NOT NULL,

    CONSTRAINT "user_shop_upgrades_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "run_upgrade_purchases" (
    "id" UUID NOT NULL,
    "run_id" UUID NOT NULL,
    "upgrade_id" VARCHAR(64) NOT NULL,
    "level" INTEGER NOT NULL,
    "floor" INTEGER NOT NULL,
    "purchase_order" INTEGER NOT NULL,

    CONSTRAINT "run_upgrade_purchases_pkey" PRIMARY KEY ("id")
);

-- CreateIndex
CREATE UNIQUE INDEX "user_shop_upgrades_user_upgrade_key" ON "user_shop_upgrades"("user_id", "upgrade_id");

-- CreateIndex
CREATE UNIQUE INDEX "run_upgrade_purchases_run_id_order_key" ON "run_upgrade_purchases"("run_id", "purchase_order");

-- AddForeignKey
ALTER TABLE "user_shop_upgrades" ADD CONSTRAINT "user_shop_upgrades_user_id_fkey" FOREIGN KEY ("user_id") REFERENCES "users"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "run_upgrade_purchases" ADD CONSTRAINT "run_upgrade_purchases_run_id_fkey" FOREIGN KEY ("run_id") REFERENCES "runs"("id") ON DELETE CASCADE ON UPDATE CASCADE;


-- Backfill: runs saved before Elif existed return this snapshot on
-- retransmission. Every balance was 0 and no upgrade existed at that time.
UPDATE "runs"
SET "elif_snapshot" = '{"elifBalance":0,"elifGained":0,"elifSpent":0,"shopUpgrades":[]}'::jsonb
WHERE "elif_snapshot" IS NULL;
