/**
 * The skill as the client module reads it, kept free of React and the client so a plain
 * `node --test` covers it (client/tests).
 * TODO: named plugin messages, once the server has them, replace the header numbers.
 */
import type { ClientModulePacketReader } from '@nitrodevco/client-module-sdk';

/** The same numbers as the server half's `SkillsHeaders`. */
export const GET_SKILL = 20400;
export const SKILL = 20401;
export const TRAIN = 20402;

export interface Skill {
    level: number;
    experience: number;
    needed: number;
}

export const NO_SKILL: Skill = { level: 1, experience: 0, needed: 100 };

/** The server's `SkillMessage`: level, experience in the level, experience the level needs. */
export const readSkill = (reader: Pick<ClientModulePacketReader, 'readInt'>): Skill => ({
    level: reader.readInt(),
    experience: reader.readInt(),
    needed: reader.readInt(),
});

/** How far through its level the skill is, 0 to 1. */
export const progress = (skill: Skill): number => (skill.needed > 0 ? Math.min(1, skill.experience / skill.needed) : 0);
