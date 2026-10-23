import assert from 'node:assert/strict';
import { test } from 'node:test';

import { progress, readSkill } from '../src/skill.ts';

void test('a skill is read in the order the server writes it', () => {
    const values = [ 3, 40, 100 ];

    assert.deepEqual(readSkill({ readInt: () => values.shift()! }), { level: 3, experience: 40, needed: 100 });
});

void test('progress is the share of the level done, never past its end', () => {
    assert.equal(progress({ level: 1, experience: 25, needed: 100 }), 0.25);
    assert.equal(progress({ level: 1, experience: 150, needed: 100 }), 1);
    assert.equal(progress({ level: 1, experience: 0, needed: 0 }), 0);
});
