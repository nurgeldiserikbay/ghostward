using UnityEngine;
using UnityEngine.InputSystem;

namespace GhostWard
{
    // Распознавание жестов предметов. Одно и то же и в игре, и на тренировке в дневнике:
    //  смахнуть вверх — соль, вниз — зеркало, двойное касание — железо, зажать палец — фонарь,
    //  зигзаг — полынь, встряхнуть телефон — колокольчик.
    public class Gestures
    {
        bool pressing, pressIgnored, moved, held;
        Vector2 pressStart, lastExtreme;
        int reversals, lastDirX;
        float pressTime, lastTapTime = -10f;
        Vector3 lastAccel;

        public bool Pressing => pressing;
        public bool Holding => held && pressing;     // фонарь горит, пока палец зажат

        public void Reset()
        {
            pressing = held = false;
            lastTapTime = -10f;
            lastAccel = ReadAccel();   // иначе поворот телефона за время паузы читается как встряхивание
        }

        // ignore — касание пришлось на кнопку интерфейса; now — время для двойного касания (в дневнике игра на паузе)
        public ItemKind? Tick(float dt, float now, System.Func<Vector2, bool> ignore)
        {
            ItemKind? result = null;
            float h = Screen.height, w = Screen.width;

            var pointer = Pointer.current;
            if (pointer != null)
            {
                bool pressed = pointer.press.isPressed;
                var pos = pointer.position.ReadValue();

                if (pressed && !pressing)
                {
                    pressing = true;
                    pressIgnored = ignore != null && ignore(pos);
                    moved = false;
                    held = false;
                    pressStart = lastExtreme = pos;
                    reversals = 0;
                    lastDirX = 0;
                    pressTime = 0f;
                }
                else if (pressed && pressing && !pressIgnored)
                {
                    pressTime += dt;
                    if ((pos - pressStart).magnitude > h * 0.03f) moved = true;
                    // зажатый неподвижный палец — фонарь
                    if (!moved && pressTime > 0.3f) held = true;

                    // зигзаг: считаем развороты по горизонтали с заметным размахом
                    float dx = pos.x - lastExtreme.x;
                    int dir = dx > 0f ? 1 : -1;
                    if (lastDirX == 0)
                    {
                        if (Mathf.Abs(dx) > w * 0.08f) { lastDirX = dir; lastExtreme = pos; }
                    }
                    else if (dir == lastDirX) lastExtreme = pos;          // тянем дальше в ту же сторону
                    else if (Mathf.Abs(dx) > w * 0.08f)
                    {
                        reversals++;
                        lastDirX = dir;
                        lastExtreme = pos;
                    }
                }
                else if (!pressed && pressing)
                {
                    pressing = false;
                    if (!pressIgnored && !held) result = OnRelease(pos - pressStart, now);
                    held = false;
                }
            }

            if (DetectShake()) result = ItemKind.Bell;
            return result;
        }

        ItemKind? OnRelease(Vector2 delta, float now)
        {
            float h = Screen.height;
            if (reversals >= 2) return ItemKind.Wormwood;
            if (pressTime < 0.6f && Mathf.Abs(delta.y) > h * 0.1f && Mathf.Abs(delta.x) < Mathf.Abs(delta.y))
                return delta.y > 0f ? ItemKind.Salt : ItemKind.Mirror;
            if (!moved && pressTime < 0.3f)
            {
                if (now - lastTapTime < 0.35f)
                {
                    lastTapTime = -10f;
                    return ItemKind.Iron;
                }
                lastTapTime = now;
            }
            return null;
        }

        static Vector3 ReadAccel()
        {
            var acc = Accelerometer.current;
            if (acc == null) return Input.acceleration;
            if (!acc.enabled) InputSystem.EnableDevice(acc);
            var a = acc.acceleration.ReadValue();
            if (a.magnitude > 4f) a /= 9.81f; // некоторые устройства отдают м/с², приводим к g
            return a;
        }

        bool DetectShake()
        {
            var a = ReadAccel();
            float jerk = (a - lastAccel).magnitude;
            lastAccel = a;
            return jerk > 1.4f;
        }
    }
}
