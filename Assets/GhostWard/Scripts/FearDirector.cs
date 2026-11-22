using UnityEngine;

namespace GhostWard
{
    // Что режиссёр страха просит сделать прямо сейчас.
    public enum Beat
    {
        None,
        Murmur,      // тихий звук на грани слышимости: нагнетание
        FalseAlarm,  // громко, но ничего нет
        Presence,    // сущность просто стоит или проходит в кадре
        Strike       // настоящий удар, один-два за визит
    }

    // Вместо независимых таймеров страх идёт волнами: долгая тишина, нарастание, развязка, передышка.
    // Цикл не прерывается между визитами, поэтому приход сущности не слышен по смене ритма.
    public sealed class FearDirector
    {
        enum Stage { Silence, Build, Recover }

        const int MaxStrikes = 2;

        Stage stage;
        float timer, murmurIn, murmurEvery;
        int strikes, payoffsSinceStrike;

        public void NewGame()
        {
            stage = Stage.Silence;
            timer = Random.Range(20f, 35f);
            NewVisit();
        }

        public void NewVisit()
        {
            strikes = 0;
            payoffsSinceStrike = 0;
        }

        // tension 0..1 — страх и злость; ramp 0..1 — нарастание по визитам;
        // canManifest — сущность сейчас расследуют, ей можно показаться
        public Beat Tick(float dt, float tension, float ramp, bool canManifest)
        {
            float pace = Mathf.Lerp(1f, 0.6f, ramp) / (1f + Mathf.Clamp01(tension) * 0.5f);
            timer -= dt;

            switch (stage)
            {
                case Stage.Silence:
                    if (timer > 0f) return Beat.None;
                    stage = Stage.Build;
                    timer = Random.Range(10f, 18f);
                    murmurEvery = timer / Random.Range(2, 4);
                    murmurIn = murmurEvery * 0.5f;
                    return Beat.None;

                case Stage.Build:
                    if (timer <= 0f)
                    {
                        stage = Stage.Recover;
                        timer = Random.Range(18f, 30f) * pace;
                        return Payoff(canManifest);
                    }
                    murmurIn -= dt;
                    if (murmurIn > 0f) return Beat.None;
                    murmurIn = murmurEvery;
                    return Beat.Murmur;

                default:
                    if (timer > 0f) return Beat.None;
                    stage = Stage.Silence;
                    timer = Random.Range(35f, 65f) * pace;
                    return Beat.None;
            }
        }

        Beat Payoff(bool canManifest)
        {
            if (!canManifest) return Beat.FalseAlarm;
            payoffsSinceStrike++;
            if (strikes < MaxStrikes && payoffsSinceStrike >= 2 && Random.value < 0.7f)
            {
                strikes++;
                payoffsSinceStrike = 0;
                return Beat.Strike;
            }
            return Random.value < 0.55f ? Beat.Presence : Beat.FalseAlarm;
        }
    }
}
