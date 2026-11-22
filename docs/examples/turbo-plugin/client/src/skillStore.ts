import { NO_SKILL, type Skill } from './skill';

/** The last skill the server sent, for the window while it is open. */
export const skillStore = {
    current: NO_SKILL,
    listeners: new Set<(skill: Skill) => void>(),
    set(skill: Skill) {
        this.current = skill;
        this.listeners.forEach(listener => listener(skill));
    },
    listen(listener: (skill: Skill) => void) {
        this.listeners.add(listener);

        return () => {
            this.listeners.delete(listener);
        };
    },
};
