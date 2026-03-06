namespace LuckySvin_DD_bot.Models
{
    public class Enemy
    {
        public string Name { get; set; } = "";

        public int HP { get; set; }

        public int RequiredRoll { get; set; }

        public int DamageMin { get; set; }
        public int DamageMax { get; set; }

        public int XPReward { get; set; }
        public int GoldReward { get; set; }

        public bool IsBoss { get; set; }
    }
}