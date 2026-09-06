-- AlterTable: Add client_profile_id column to users
-- For existing rows, generate a UUID to fill the required field

-- Add nullable column first
ALTER TABLE "users" ADD COLUMN "client_profile_id" UUID;

-- Fill existing rows with generated UUIDs
UPDATE "users" SET "client_profile_id" = gen_random_uuid() WHERE "client_profile_id" IS NULL;

-- Make column required
ALTER TABLE "users" ALTER COLUMN "client_profile_id" SET NOT NULL;

-- Add unique constraint
ALTER TABLE "users" ADD CONSTRAINT "users_client_profile_id_key" UNIQUE ("client_profile_id");
