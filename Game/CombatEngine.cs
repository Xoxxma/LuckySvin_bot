using System;
using LuckySvin_DD_bot.Models;

namespace LuckySvin_DD_bot.Game
{
    public enum EnemyKind
    {
        GoblinSpearman,
        ForestRaider,
        ForestLord
    }

    public class CombatEngine
    {
        private readonly Random _random = new();

        public Enemy CreateEnemy(EnemyKind kind)
        {
            return kind switch
            {
                EnemyKind.GoblinSpearman => new Enemy
                {
                    Name = "Goblin Spearman",
                    HP = 16,
                    RequiredRoll = 6,
                    DamageMin = 1,
                    DamageMax = 4,
                    XPReward = 8,
                    GoldReward = 3,
                    IsBoss = false
                },

                EnemyKind.ForestRaider => new Enemy
                {
                    Name = "Forest Raider",
                    HP = 26,
                    RequiredRoll = 7,
                    DamageMin = 2,
                    DamageMax = 6,
                    XPReward = 16,
                    GoldReward = 6,
                    IsBoss = false
                },

                _ => new Enemy
                {
                    Name = "Forest Lord",
                    HP = 95,
                    RequiredRoll = 12,
                    DamageMin = 4,
                    DamageMax = 9,
                    XPReward = 70,
                    GoldReward = 25,
                    IsBoss = true
                }
            };
        }

        public static string LocalEnemyName(EnemyKind kind, Language lang)
        {
            return (kind, lang) switch
            {
                (EnemyKind.GoblinSpearman, Language.UA) => "Гоблін-списник",
                (EnemyKind.GoblinSpearman, Language.RU) => "Гоблин-копейщик",
                (EnemyKind.GoblinSpearman, Language.EN) => "Goblin Spearman",

                (EnemyKind.ForestRaider, Language.UA) => "Лісовий рейдер",
                (EnemyKind.ForestRaider, Language.RU) => "Лесной рейдер",
                (EnemyKind.ForestRaider, Language.EN) => "Forest Raider",

                (EnemyKind.ForestLord, Language.UA) => "Лісовий Лорд",
                (EnemyKind.ForestLord, Language.RU) => "Лесной Лорд",
                (EnemyKind.ForestLord, Language.EN) => "Forest Lord",

                _ => "Enemy"
            };
        }

        public string Attack(Player p, int d20)
        {
            var enemy = p.CurrentEnemy!;
            string text = Local(p.Lang,
                $"🎲 Кидок d20: {d20}\n",
                $"🎲 Бросок d20: {d20}\n",
                $"🎲 d20 roll: {d20}\n");

            // 20 = instant win
            if (d20 == 20)
            {
                enemy.HP = 0;
                text += Local(p.Lang,
                    "🌟 Натуральна 20! Миттєва перемога!",
                    "🌟 Натуральная 20! Мгновенная победа!",
                    "🌟 Natural 20! Instant victory!");
                text += StatusLine(p, enemy);
                return text;
            }

            // 1 = instant lose
            if (d20 == 1)
            {
                p.HP = 0;
                text += Local(p.Lang,
                    "💀 Натуральна 1! Миттєва поразка!",
                    "💀 Натуральная 1! Мгновенное поражение!",
                    "💀 Natural 1! Instant defeat!");
                text += StatusLine(p, enemy);
                return text;
            }

            int baseDamage = _random.Next(4, 8) + p.Level;

            if (d20 >= enemy.RequiredRoll)
            {
                enemy.HP -= baseDamage;
                text += Local(p.Lang,
                    $"✔ Ти завдав {baseDamage} шкоди.",
                    $"✔ Ты нанёс {baseDamage} урона.",
                    $"✔ You deal {baseDamage} damage.");
            }
            else
            {
                text += Local(p.Lang,
                    "❌ Промах.",
                    "❌ Промах.",
                    "❌ Miss.");
            }

            if (enemy.HP > 0)
            {
                int enemyDamage = _random.Next(enemy.DamageMin, enemy.DamageMax + 1);
                p.HP -= enemyDamage;
                text += Local(p.Lang,
                    $"\n👹 Ворог б'є на {enemyDamage}.",
                    $"\n👹 Враг бьёт на {enemyDamage}.",
                    $"\n👹 Enemy hits for {enemyDamage}.");
            }

            text += StatusLine(p, enemy);
            return text;
        }

        private static string StatusLine(Player p, Enemy enemy)
        {
            return Local(p.Lang,
                $"\n\n❤️ Ти: {Math.Max(0, p.HP)} / {p.MaxHP} | 👹 Ворог: {Math.Max(0, enemy.HP)}",
                $"\n\n❤️ Ты: {Math.Max(0, p.HP)} / {p.MaxHP} | 👹 Враг: {Math.Max(0, enemy.HP)}",
                $"\n\n❤️ You: {Math.Max(0, p.HP)} / {p.MaxHP} | 👹 Enemy: {Math.Max(0, enemy.HP)}");
        }

        private static string Local(Language lang, string ua, string ru, string en)
        {
            return lang switch
            {
                Language.UA => ua,
                Language.RU => ru,
                _ => en
            };
        }
    }
}
