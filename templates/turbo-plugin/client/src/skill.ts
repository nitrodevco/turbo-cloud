/**
 * The skill as the client module reads it, kept free of React and the client so a plain
 * `node --test` covers it (client/tests). The messages are the server half's
 * (`server/SkillsMessages.cs`), read and sent by name: the server sends their headers and fields.
 */
import type { ClientModuleMessageOf } from '@nitrodevco/client-module-sdk';

/** Client to server: the window wants the player's skill. */
export const GET_SKILL = 'skills.get';

/** Client to server: the window's Train button. */
export const TRAIN = 'skills.train';

/** Server to client: the player's skill. */
export const SKILL = 'skills.state';

/** `skills.state`'s fields, as the server sends its schema (`SkillMessage`'s parameters, camelCase). */
export const SKILL_SCHEMA = [ [ 'level', 'int' ], [ 'experience', 'int' ], [ 'needed', 'int' ] ] as const;

export type Skill = ClientModuleMessageOf<typeof SKILL_SCHEMA>;

export const NO_SKILL: Skill = { level: 1, experience: 0, needed: 100 };

const number = (value: unknown, fallback: number) => ((typeof value === 'number') && Number.isFinite(value) ? value : fallback);

/** `skills.state` as the client hands it over: an object of its fields, each a number. */
export const toSkill = (data: Record<string, unknown>): Skill => ({
    level: number(data.level, NO_SKILL.level),
    experience: number(data.experience, 0),
    needed: number(data.needed, 0),
});

/** How far through its level the skill is, 0 to 1. */
export const progress = (skill: Skill): number => (skill.needed > 0 ? Math.min(1, skill.experience / skill.needed) : 0);
