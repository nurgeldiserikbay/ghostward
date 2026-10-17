using UnityEngine;
using UnityEngine.InputSystem;

namespace GhostWard
{
    // Шагомер для режима гироскопа. Без ARCore игра знает только поворот телефона — где игрок, она не знает.
    // Шаги по акселерометру дают это приблизительно: каждый шаг — ~0.6 м туда, куда смотрит камера
    // (Game.OnSteps). Так сущность остаётся на своём месте в комнате: подошёл — она ближе, отошёл — дальше.
    // Шаг — ровный толчок 0.1–0.6 g; ходьба начинается с трёх толчков в ровном ритме 0.3–0.95 с
    // (иначе дрожь руки двигала бы игрока), дальше каждый толчок в ритме — шаг.
    // Встряхивание (колокольчик) сильнее и беспорядочнее — его не путаем.
    public class StepDetector
    {
        const float High = 0.13f, Low = 0.06f, Shake = 0.75f;
        const int StartSteps = 3;

        int count;          // толчков подряд в ритме
        float lastPeak = -10f;
        float smooth;
        bool above;

        public bool Walking => count >= StartSteps;

        public void Reset()
        {
            count = 0;
            smooth = 0f;
            above = false;
        }

        // сколько новых шагов за этот кадр; suppress — идёт жест предмета или резкий поворот
        public int Tick(float dt, float now, bool suppress)
        {
#if UNITY_EDITOR
            if (Keyboard.current != null && Keyboard.current.wKey.wasPressedThisFrame) return 1;   // проверка в редакторе
#endif
            if (suppress || !SystemInfo.supportsGyroscope) { Reset(); return 0; }
            float a = Input.gyro.userAcceleration.magnitude;   // без силы тяжести, в g
            if (a > Shake) { Reset(); return 0; }
            smooth = Mathf.Lerp(smooth, a, 1f - Mathf.Exp(-dt * 18f));

            if (!above && smooth > High)
            {
                above = true;
                float gap = now - lastPeak;
                if (gap < 0.28f) return 0;   // тот же толчок
                lastPeak = now;
                if (gap > 0.95f) { count = 1; return 0; }   // ритм сбился — начинаем заново
                count++;
                if (count == StartSteps) return StartSteps;   // ходьба подтвердилась — засчитываем и первые шаги
                return count > StartSteps ? 1 : 0;
            }
            if (above && smooth < Low) above = false;
            return 0;
        }
    }
}
