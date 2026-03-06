namespace LuckySvin_DD_bot.Models
{
    public class Player
    {
        public long UserId { get; set; }

        public Language Lang { get; set; } = Language.UA;

        public int HP { get; set; } = 20;
        public int MaxHP { get; set; } = 20;

        public int Level { get; set; } = 1;
        public int XP { get; set; } = 0;
        public int XPToNextLevel { get; set; } = 20;

        public int Gold { get; set; } = 0;
        public int Potions { get; set; } = 1;

        public int Position { get; set; } = 0;

        public bool InFight { get; set; }

        public Enemy? CurrentEnemy { get; set; }
    }
}