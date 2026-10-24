import { type ClientModuleWindowProps, useApi } from '@nitrodevco/client-module-sdk';
import { Box, Button, Frame, ThemeText } from '@nitrodevco/client-module-sdk/theme';
import { useEffect, useState } from 'react';

import { GET_SKILL, progress, type Skill, TRAIN } from './skill';
import { skillStore } from './skillStore';

/** The plugin's window: the player's level, how far through it they are, and the Train button. */
export const SkillsWindow = ({ close }: ClientModuleWindowProps) => {
    const api = useApi();
    const [ skill, setSkill ] = useState<Skill>(skillStore.current);

    useEffect(() => {
        const stop = skillStore.listen(setSkill);

        api.messages.send(GET_SKILL, {});

        return stop;
    }, [ api ]);

    const text = (key: string, fallback: string, replacements?: Record<string, string>) => api.texts.get(`skills.window.${key}`, fallback, replacements);
    const done = Math.round(progress(skill) * 100);

    return (
        <Frame
            id={`${api.key}-skills`}
            variant="3"
            caption={text('title', 'Skills')}
            centered
            resizeDirection="none"
            onClose={close}
            layout={{ width: 240, height: 140 }}>
            <Box layout={{ flexDirection: 'column', gap: 8, padding: 10, width: 238 }}>
                <ThemeText
                    text={text('level', 'Level %level%', { level: String(skill.level) })}
                    textStyle="u_bold" />
                <ThemeText
                    text={text('experience', '%experience% / %needed% experience', { experience: String(skill.experience), needed: String(skill.needed) }) + ` (${done}%)`}
                    textStyle="u_regular" />
                <Button
                    variant="101"
                    onPointerTap={() => api.messages.send(TRAIN, {})}
                    layout={{ width: 100, height: 22 }}>
                    {text('train', 'Train')}
                </Button>
            </Box>
        </Frame>
    );
};
