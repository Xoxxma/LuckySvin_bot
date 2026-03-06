using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace LuckySvin_DD_bot.Services
{
    /// <summary>
    /// Read-only mapping: d20 result (1..20) -> Telegram sticker file_id.
    /// (Binding is disabled in Program.cs.)
    /// </summary>
    public sealed class DiceStickerService
    {
        private readonly Dictionary<int, string> _map;

        public DiceStickerService(string path)
        {
            _map = Load(path);
        }

        public bool TryGet(int value, out string fileId) => _map.TryGetValue(value, out fileId!);

        private static Dictionary<int, string> Load(string path)
        {
            try
            {
                if (!File.Exists(path))
                    return new Dictionary<int, string>();

                var json = File.ReadAllText(path);
                var dict = JsonSerializer.Deserialize<Dictionary<int, string>>(json);

                return dict ?? new Dictionary<int, string>();
            }
            catch
            {
                return new Dictionary<int, string>();
            }
        }
    }
}
