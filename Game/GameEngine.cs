using LuckySvin_DD_bot.Models;

namespace LuckySvin_DD_bot.Game
{
    public class GameEngine
    {
        private readonly Random _random = new();

        public string HandleMove(Player player, int diceValue)
        {
            player.Position += diceValue;

            if (player.Position >= 50)
                return "BOSS";

            if (_random.Next(0, 100) < 40)
                return "COMBAT";

            return "SAFE";
        }
    }
}