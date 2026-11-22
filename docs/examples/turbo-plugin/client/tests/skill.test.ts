import assert from 'node:assert/strict';
import { test } from 'node:test';

import { NO_SKILL, progress, toSkill } from '../src/skill.ts';

void test('a skill is read from the fields the server names', () => {
    assert.deepEqual(toSkill({ level: 3, experience: 40, needed: 100 }), { level: 3, experience: 40, needed: 100 });
});

void test('a field the server did not send keeps its default', () => {
    assert.deepEqual(toSkill({ experience: 5 }), { level: NO_SKILL.level, experience: 5, needed: 0 });
});

void test('progress is the share of the level done, never past its end', () => {
    assert.equal(progress({ level: 1, experience: 25, needed: 100 }), 0.25);
    assert.equal(progress({ level: 1, experience: 150, needed: 100 }), 1);
    assert.equal(progress({ level: 1, experience: 0, needed: 0 }), 0);
});
