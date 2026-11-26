using System;
using UnityEngine;

namespace GhostWard
{
    public enum Lang { En, Ru, Kk, Es, Pt }

    // Локализация: ключ → строка на текущем языке (таблица в Strings.cs). По умолчанию английский.
    public static class Loc
    {
        // Название вынесено сюда: сменить бренд — одна строка. Вторая часть названия переводится (ключ title_sub).
        public const string Brand = "Ghost Ward";

        public static readonly string[] LangNames = { "English", "Русский", "Қазақша", "Español", "Português" };

        public static Lang Current { get; private set; } = (Lang)PlayerPrefs.GetInt("lang", 0);
        public static bool Chosen => PlayerPrefs.HasKey("lang");
        public static event Action Changed;

        public static void Set(Lang lang)
        {
            Current = lang;
            PlayerPrefs.SetInt("lang", (int)lang);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        public static string T(string key)
        {
            if (!Strings.Table.TryGetValue(key, out var row)) return key;
            int i = (int)Current;
            return i < row.Length && !string.IsNullOrEmpty(row[i]) ? row[i] : row[0];
        }

        public static string F(string key, params object[] args) => string.Format(T(key), args);

        public static string Title => $"{Brand}: {T("title_sub")}";
    }
}
