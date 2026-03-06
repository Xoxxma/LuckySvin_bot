using LuckySvin_DD_bot.Models;

namespace LuckySvin_DD_bot.Services
{
    public class LocalizationService
    {
        public string HUD(Player p)
        {
            return
                $"❤️ HP: {p.HP}/{p.MaxHP}\n" +
                $"⭐ Level: {p.Level} XP: {p.XP}/{p.XPToNextLevel}\n" +
                $"🧪 Potions: {p.Potions}\n" +
                $"💰 Gold: {p.Gold}\n" +
                $"📍 Position: {p.Position}/50";
        }
    }
}