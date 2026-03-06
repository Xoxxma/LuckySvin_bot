using System.Collections.Generic;
using LuckySvin_DD_bot.Models;

namespace LuckySvin_DD_bot.Services
{
    public class PlayerService
    {
        private readonly Dictionary<long, Player> _players = new();

        public Player GetOrCreatePlayer(long userId)
        {
            if (!_players.ContainsKey(userId))
            {
                _players[userId] = new Player
                {
                    UserId = userId
                };
            }

            return _players[userId];
        }

        public Player? GetPlayer(long userId)
        {
            _players.TryGetValue(userId, out var player);
            return player;
        }
    }
}