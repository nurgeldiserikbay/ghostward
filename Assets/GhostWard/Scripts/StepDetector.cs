using UnityEngine;
using UnityEngine.InputSystem;

namespace GhostWard
{
    // Шаги по акселерометру (режим гироскопа). Без ARCore игра знает только поворот телефона:
    // если игрок пошёл, сущности «едут» вместе с ним, а карта комнаты перестаёт совпадать с комнатой.
    // Шаг — ровный толчок 0.1–0.6 g; ходьба — четыре толчка подряд с ровным ритмом 0.3–0.9 с.
    // Встряхивание (колокольчик) сильнее и беспорядочнее — его не путаем.
    public class StepDetector
    {
        const float High = 0.13f, Low = 0.06f, Shake = 0.75f;

        readonly float[] peaks = new float[4];
        int count;
        float smooth;
        bool above;

        public void Reset()
        {
            count = 0;
            smooth = 0f;
            above = false;
        }

        // true — игрок сейчас идёт; suppress — идёт жест предмета или касание
        public bool Tick(float dt, float now, bool suppress)
        {
#if UNITY_EDITOR
            if (Keyboard.current != null && Keyboard.current.wKey.wasPressedThisFrame) return true;   // проверка в редакторе
#endif
            if (suppress || !SystemInfo.supportsGyroscope) { Reset(); return false; }
            float a = Input.gyro.userAcceleration.magnitude;   // без силы тяжести, в g
            if (a > Shake) { Reset(); return false; }
            smooth = Mathf.Lerp(smooth, a, 1f - Mathf.Exp(-dt * 18f));

            if (!above && smooth > High)
            {
                above = true;
                // ритм сбился — начинаем счёт заново
                if (count > 0)
                {
                    float gap = now - peaks[(count - 1) % peaks.Length];
                    if (gap < 0.28f) return false;          // тот же толчок
                    if (gap > 0.95f) count = 0;
                }
                peaks[count % peaks.Length] = now;
                count++;
                if (count >= peaks.Length)
                {
                    count = 0;
                    return true;
                }
            }
            else if (above && smooth < Low) above = false;
            return false;
        }
    }
}
