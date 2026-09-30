ALTER TABLE "runs"
ADD COLUMN "client_run_id" UUID,
ADD COLUMN "request_fingerprint" CHAR(64),
ADD COLUMN "experience_gained" INTEGER NOT NULL DEFAULT 0,
ADD COLUMN "progress_snapshot" JSONB;

UPDATE "runs"
SET "client_run_id" = "id",
    "request_fingerprint" = md5("id"::text) || md5("id"::text);

ALTER TABLE "runs"
ALTER COLUMN "client_run_id" SET NOT NULL,
ALTER COLUMN "request_fingerprint" SET NOT NULL;

CREATE UNIQUE INDEX "runs_client_run_id_key" ON "runs"("client_run_id");

ALTER TABLE "runs"
ADD CONSTRAINT "runs_experience_gained_nonnegative_check"
CHECK ("experience_gained" >= 0);

CREATE TABLE "user_character_progress" (
    "id" UUID NOT NULL,
    "user_id" UUID NOT NULL,
    "character_id" VARCHAR(64) NOT NULL,
    "level" INTEGER NOT NULL DEFAULT 1,
    "experience" INTEGER NOT NULL DEFAULT 0,
    "skill_points" INTEGER NOT NULL DEFAULT 0,
    "low_grade_skill_level" INTEGER NOT NULL DEFAULT 1,
    "high_grade_skill_level" INTEGER NOT NULL DEFAULT 1,
    "created_at" TIMESTAMPTZ(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "updated_at" TIMESTAMPTZ(3) NOT NULL,

    CONSTRAINT "user_character_progress_pkey" PRIMARY KEY ("id"),
    CONSTRAINT "user_character_progress_level_check" CHECK ("level" BETWEEN 1 AND 19),
    CONSTRAINT "user_character_progress_experience_check" CHECK ("experience" >= 0),
    CONSTRAINT "user_character_progress_skill_points_check" CHECK ("skill_points" >= 0),
    CONSTRAINT "user_character_progress_low_skill_check" CHECK ("low_grade_skill_level" BETWEEN 1 AND 10),
    CONSTRAINT "user_character_progress_high_skill_check" CHECK ("high_grade_skill_level" BETWEEN 1 AND 10)
);

CREATE UNIQUE INDEX "user_character_progress_user_character_key"
ON "user_character_progress"("user_id", "character_id");

CREATE INDEX "user_character_progress_character_id_idx"
ON "user_character_progress"("character_id");

ALTER TABLE "user_character_progress"
ADD CONSTRAINT "user_character_progress_user_id_fkey"
FOREIGN KEY ("user_id") REFERENCES "users"("id") ON DELETE CASCADE ON UPDATE CASCADE;

ALTER TABLE "user_character_progress"
ADD CONSTRAINT "user_character_progress_character_id_fkey"
FOREIGN KEY ("character_id") REFERENCES "characters"("id") ON DELETE RESTRICT ON UPDATE CASCADE;
