/*
  Warnings:

  - Added the required column `game_version` to the `runs` table without a default value. This is not possible if the table is not empty.

*/
-- AlterTable
ALTER TABLE "runs" ADD COLUMN     "game_version" VARCHAR(32) NOT NULL;
