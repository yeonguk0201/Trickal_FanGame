-- Rename the pre-release placeholder character ID while preserving existing Run relations.
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM "characters" WHERE "id" = 'character-a')
       AND NOT EXISTS (SELECT 1 FROM "characters" WHERE "id" = 'erpin') THEN
        UPDATE "characters"
        SET "id" = 'erpin',
            "name" = '에르핀',
            "description" = 'MVP 기본 플레이 캐릭터',
            "updated_at" = CURRENT_TIMESTAMP
        WHERE "id" = 'character-a';
    ELSIF EXISTS (SELECT 1 FROM "characters" WHERE "id" = 'character-a') THEN
        UPDATE "runs"
        SET "character_id" = 'erpin'
        WHERE "character_id" = 'character-a';

        DELETE FROM "characters" WHERE "id" = 'character-a';
    END IF;

    IF NOT EXISTS (SELECT 1 FROM "characters" WHERE "id" = 'erpin') THEN
        INSERT INTO "characters" (
            "id",
            "name",
            "description",
            "is_active",
            "created_at",
            "updated_at"
        ) VALUES (
            'erpin',
            '에르핀',
            'MVP 기본 플레이 캐릭터',
            true,
            CURRENT_TIMESTAMP,
            CURRENT_TIMESTAMP
        );
    ELSE
        UPDATE "characters"
        SET "name" = '에르핀',
            "description" = 'MVP 기본 플레이 캐릭터',
            "is_active" = true,
            "updated_at" = CURRENT_TIMESTAMP
        WHERE "id" = 'erpin';
    END IF;
END $$;
