-- Stable ranking order and per-user aggregate access paths.
CREATE INDEX "runs_highest_floor_ranking_idx"
ON "runs" ("reached_floor" DESC, "play_time" ASC, "ended_at" ASC, "id" ASC);

CREATE INDEX "runs_fastest_clear_ranking_idx"
ON "runs" ("is_cleared", "play_time" ASC, "ended_at" ASC, "id" ASC);

CREATE INDEX "runs_user_clear_idx"
ON "runs" ("user_id", "is_cleared");
