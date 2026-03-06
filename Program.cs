using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LuckySvin_DD_bot.Game;
using LuckySvin_DD_bot.Models;
using LuckySvin_DD_bot.Services;
using Microsoft.Extensions.Configuration;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace LuckySvin_DD_bot
{
    internal class Program
    {
        private static TelegramBotClient bot = null!;
        private static readonly Dictionary<long, Session> sessions = new();

        private static readonly StoryService Story = new();
        private static readonly CombatEngine Combat = new();
        private static DiceStickerService DiceStickers = null!;

        static async Task Main()
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false)
                .Build();

            string token = config["BotSettings:Token"]!;
            bot = new TelegramBotClient(token);

            DiceStickers = CreateDiceStickerService();

            using var cts = new CancellationTokenSource();

            var receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = Array.Empty<UpdateType>()
            };

            bot.StartReceiving(
                HandleUpdateAsync,
                HandleErrorAsync,
                receiverOptions,
                cancellationToken: cts.Token
            );

            Console.WriteLine("LuckySvin started...");
            Console.ReadLine();
        }

        private static DiceStickerService CreateDiceStickerService()
        {
            string currentDir = Directory.GetCurrentDirectory();

            string d20Path = Path.Combine(currentDir, "d20_stickers.json");
            if (File.Exists(d20Path))
                return new DiceStickerService(d20Path);

            string oldPath = Path.Combine(currentDir, "dice_stickers.json");
            if (File.Exists(oldPath))
                return new DiceStickerService(oldPath);

            return new DiceStickerService(d20Path);
        }

        private static Session GetOrCreateSession(long chatId)
        {
            if (!sessions.TryGetValue(chatId, out var s))
            {
                s = new Session
                {
                    Player = new Player { UserId = chatId },
                    State = GameState.MainMenu,
                    SceneId = StoryService.StartSceneId
                };
                sessions[chatId] = s;
            }

            return s;
        }

        private static async Task HandleUpdateAsync(
            ITelegramBotClient botClient,
            Update update,
            CancellationToken cancellationToken)
        {
            if (update.Type == UpdateType.CallbackQuery && update.CallbackQuery != null)
            {
                await HandleCallbackAsync(botClient, update.CallbackQuery, cancellationToken);
                return;
            }

            if (update.Message?.Text == null)
                return;

            var chatId = update.Message.Chat.Id;
            var text = update.Message.Text.Trim();

            var session = GetOrCreateSession(chatId);
            var p = session.Player;

            text = NormalizeInput(text);

            if (text == "/start")
            {
                session.State = GameState.MainMenu;

                var keyboard = new ReplyKeyboardMarkup(new[]
                {
                    new KeyboardButton[] { "Українська", "Русский", "English" }
                })
                { ResizeKeyboard = true };

                await botClient.SendMessage(
                    chatId,
                    "🎲 Choose language / Обери мову / Выбери язык",
                    replyMarkup: keyboard,
                    cancellationToken: cancellationToken);

                return;
            }

            if (session.State == GameState.MainMenu)
            {
                if (text == "Українська") p.Lang = Language.UA;
                else if (text == "Русский") p.Lang = Language.RU;
                else if (text == "English") p.Lang = Language.EN;
                else
                {
                    await botClient.SendMessage(
                        chatId,
                        "Choose: Українська / Русский / English",
                        cancellationToken: cancellationToken);
                    return;
                }

                StartNewRun(session);

                await botClient.SendMessage(
                    chatId,
                    StoryService.Local(p.Lang,
                        "🎮 Керування оновлено. Кнопки знизу вже готові.",
                        "🎮 Управление обновлено. Кнопки снизу уже готовы.",
                        "🎮 Controls updated. The buttons below are ready."),
                    replyMarkup: BuildGameKeyboard(p.Lang),
                    cancellationToken: cancellationToken);

                await SendSceneAsync(botClient, chatId, session, cancellationToken);
                return;
            }

            if (text == "/help")
            {
                await botClient.SendMessage(
                    chatId,
                    StoryService.Local(p.Lang,
                        "Команди: /start, /attack (у бою), /potion (у бою), /hud, /help",
                        "Команды: /start, /attack (в бою), /potion (в бою), /hud, /help",
                        "Commands: /start, /attack (in combat), /potion (in combat), /hud, /help"),
                    cancellationToken: cancellationToken);
                return;
            }

            if (text == "/hud")
            {
                await botClient.SendMessage(
                    chatId,
                    BuildHudText(p),
                    cancellationToken: cancellationToken);
                return;
            }

            if (text == "/potion")
            {
                await HandlePotionAsync(botClient, chatId, session, cancellationToken);
                return;
            }

            if (text == "/attack")
            {
                await HandleAttackAsync(botClient, chatId, session, cancellationToken);
                return;
            }

            if (session.State == GameState.Story)
            {
                await botClient.SendMessage(
                    chatId,
                    StoryService.Local(p.Lang,
                        "Натисни кнопку під текстом сцени.",
                        "Нажми кнопку под текстом сцены.",
                        "Tap the button under the scene text."),
                    cancellationToken: cancellationToken);
                return;
            }

            if (session.State == GameState.Combat)
            {
                await botClient.SendMessage(
                    chatId,
                    StoryService.Local(p.Lang,
                        "У бою: /attack або /potion",
                        "В бою: /attack или /potion",
                        "In combat: /attack or /potion"),
                    cancellationToken: cancellationToken);
            }
        }


        private static string NormalizeInput(string text)
        {
            return text switch
            {
                "⚔ Атака" => "/attack",
                "⚔ Attack" => "/attack",

                "🧪 Випити зілля" => "/potion",
                "🧪 Выпить хилку" => "/potion",
                "🧪 Drink potion" => "/potion",

                "📊 Статус" => "/hud",
                "📊 Status" => "/hud",

                "❓ Допомога" => "/help",
                "❓ Помощь" => "/help",
                "❓ Help" => "/help",

                _ => text
            };
        }

        private static ReplyKeyboardMarkup BuildGameKeyboard(Language lang)
        {
            string attack = StoryService.Local(lang, "⚔ Атака", "⚔ Атака", "⚔ Attack");
            string potion = StoryService.Local(lang, "🧪 Випити зілля", "🧪 Выпить хилку", "🧪 Drink potion");
            string hud = StoryService.Local(lang, "📊 Статус", "📊 Статус", "📊 Status");
            string help = StoryService.Local(lang, "❓ Допомога", "❓ Помощь", "❓ Help");

            return new ReplyKeyboardMarkup(new[]
            {
                new KeyboardButton[] { attack, potion, hud },
                new KeyboardButton[] { help }
            })
            {
                ResizeKeyboard = true
            };
        }

        private static void StartNewRun(Session session)
        {
            var p = session.Player;

            p.HP = p.MaxHP;
            p.Level = 1;
            p.XP = 0;
            p.XPToNextLevel = 20;
            p.Gold = 0;
            p.Potions = 1;
            p.Position = 0;
            p.InFight = false;
            p.CurrentEnemy = null;

            session.State = GameState.Story;
            session.SceneId = StoryService.StartSceneId;

            session.Ally = Ally.None;
            session.ShopDiscount = 0;

            session.CurrentCombatKind = EnemyKind.GoblinSpearman;
            session.UsedPotionThisCombat = false;
            session.PromptedPotionThisCombat = false;

            session.RaidersEncounterActive = false;
            session.RaidersDefeated = 0;
            session.UsedPotionThisRaidersEncounter = false;
            session.NextSceneAfterCombat = null;
        }

        private static async Task HandleCallbackAsync(
            ITelegramBotClient botClient,
            Telegram.Bot.Types.CallbackQuery cb,
            CancellationToken ct)
        {
            var chatId = cb.Message!.Chat.Id;
            var session = GetOrCreateSession(chatId);

            string data = cb.Data ?? "";
            var parts = data.Split('|', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length >= 3 && parts[0] == "ch")
            {
                string sceneId = parts[1];
                string choiceKey = parts[2];

                var transition = Story.ApplyChoice(session, sceneId, choiceKey);

                switch (transition.Kind)
                {
                    case TransitionKind.GoToScene:
                        session.SceneId = transition.NextSceneId!;
                        session.State = GameState.Story;
                        await SendSceneAsync(botClient, chatId, session, ct);
                        break;

                    case TransitionKind.OpenShop:
                        session.State = GameState.Shop;
                        await SendShopAsync(botClient, chatId, session, ct);
                        break;

                    case TransitionKind.StartCombat:
                        StartCombat(session, transition.CombatEnemyKind!.Value, transition.NextSceneAfterCombat);
                        await SendCombatIntroAsync(botClient, chatId, session, ct);
                        break;
                }

                await botClient.AnswerCallbackQuery(cb.Id, cancellationToken: ct);
                return;
            }

            if (parts.Length >= 2 && parts[0] == "shop")
            {
                switch (parts[1])
                {
                    case "haggle":
                        await DoHaggleAsync(botClient, chatId, session, ct);
                        break;
                    case "buy_potion":
                        await BuyPotionAsync(botClient, chatId, session, ct);
                        break;
                    case "leave":
                        session.State = GameState.Story;
                        session.SceneId = "SC08";
                        await SendSceneAsync(botClient, chatId, session, ct);
                        break;
                }

                await botClient.AnswerCallbackQuery(cb.Id, cancellationToken: ct);
                return;
            }
        }

        private static void StartCombat(Session session, EnemyKind kind, string? nextSceneAfterCombat)
        {
            var p = session.Player;

            session.State = GameState.Combat;
            session.NextSceneAfterCombat = nextSceneAfterCombat;

            session.CurrentCombatKind = kind;
            session.UsedPotionThisCombat = false;
            session.PromptedPotionThisCombat = false;

            if (kind == EnemyKind.ForestRaider && !session.RaidersEncounterActive)
            {
                session.RaidersEncounterActive = true;
                session.RaidersDefeated = 0;
                session.UsedPotionThisRaidersEncounter = false;
            }

            p.InFight = true;
            p.CurrentEnemy = Combat.CreateEnemy(kind);
            p.CurrentEnemy.Name = CombatEngine.LocalEnemyName(kind, p.Lang);

            if (session.Ally == Ally.Scout && p.CurrentEnemy != null && !p.CurrentEnemy.IsBoss)
            {
                p.CurrentEnemy.RequiredRoll = Math.Max(2, p.CurrentEnemy.RequiredRoll - 1);
            }

            if (kind != EnemyKind.ForestRaider)
            {
                session.RaidersEncounterActive = false;
                session.RaidersDefeated = 0;
                session.UsedPotionThisRaidersEncounter = false;
            }
        }

        private static async Task SendCombatIntroAsync(
            ITelegramBotClient botClient,
            long chatId,
            Session session,
            CancellationToken ct)
        {
            var p = session.Player;
            var e = p.CurrentEnemy!;

            string text = StoryService.Local(p.Lang,
                $"⚔ Бій починається! Ворог: {e.Name}\nКоманди: /attack, /potion",
                $"⚔ Бой начинается! Враг: {e.Name}\nКоманды: /attack, /potion",
                $"⚔ Combat starts! Enemy: {e.Name}\nCommands: /attack, /potion");

            await botClient.SendMessage(chatId, text, cancellationToken: ct);
        }

        private static async Task HandleAttackAsync(
            ITelegramBotClient botClient,
            long chatId,
            Session session,
            CancellationToken ct)
        {
            if (session.State != GameState.Combat || session.Player.CurrentEnemy == null)
            {
                await botClient.SendMessage(
                    chatId,
                    StoryService.Local(session.Player.Lang,
                        "Зараз ти не в бою.",
                        "Сейчас ты не в бою.",
                        "You're not in combat right now."),
                    cancellationToken: ct);
                return;
            }

            var p = session.Player;
            int roll = Random.Shared.Next(1, 21);

            bool stickerSent = false;

            if (DiceStickers.TryGet(roll, out var stickerId))
            {
                try
                {
                    await botClient.SendSticker(
                        chatId,
                        InputFile.FromFileId(stickerId),
                        cancellationToken: ct);
                    stickerSent = true;
                }
                catch
                {
                    stickerSent = false;
                }
            }

            if (!stickerSent)
            {
                await botClient.SendMessage(
                    chatId,
                    StoryService.Local(p.Lang,
                        $"🎲 Кидок d20: {roll}",
                        $"🎲 Бросок d20: {roll}",
                        $"🎲 d20 roll: {roll}"),
                    cancellationToken: ct);
            }

            string combatText = Combat.Attack(p, roll);
            await botClient.SendMessage(chatId, combatText, cancellationToken: ct);

            if (p.HP <= 0)
            {
                EndCombat(session);
                session.SceneId = ResolveAfterCombatScene(session, playerWon: false);
                session.State = GameState.Story;
                await SendSceneAsync(botClient, chatId, session, ct);
                return;
            }

            if (p.CurrentEnemy!.HP <= 0)
            {
                ApplyRewards(p, p.CurrentEnemy);
                await botClient.SendMessage(chatId, RewardText(p, p.CurrentEnemy), cancellationToken: ct);

                EndCombat(session);
                session.SceneId = ResolveAfterCombatScene(session, playerWon: true);
                session.State = GameState.Story;
                await SendSceneAsync(botClient, chatId, session, ct);
                return;
            }

            if (session.CurrentCombatKind == EnemyKind.GoblinSpearman
                && p.Potions > 0
                && !session.UsedPotionThisCombat
                && !session.PromptedPotionThisCombat
                && p.HP <= 5)
            {
                session.PromptedPotionThisCombat = true;

                await botClient.SendMessage(
                    chatId,
                    StoryService.Local(p.Lang,
                        "🧪 Критичний момент! Можеш випити зілля: /potion",
                        "🧪 Критический момент! Можешь выпить хилку: /potion",
                        "🧪 Critical moment! You can drink a potion: /potion"),
                    cancellationToken: ct);
            }
        }

        private static async Task HandlePotionAsync(
            ITelegramBotClient botClient,
            long chatId,
            Session session,
            CancellationToken ct)
        {
            var p = session.Player;

            if (session.State != GameState.Combat || p.CurrentEnemy == null)
            {
                await botClient.SendMessage(
                    chatId,
                    StoryService.Local(p.Lang,
                        "Зілля можна використовувати тільки в бою.",
                        "Хилку можно использовать только в бою.",
                        "You can use a potion only in combat."),
                    cancellationToken: ct);
                return;
            }

            if (p.Potions <= 0)
            {
                await botClient.SendMessage(
                    chatId,
                    StoryService.Local(p.Lang,
                        "У тебе немає зілля.",
                        "У тебя нет хилок.",
                        "You have no potions."),
                    cancellationToken: ct);
                return;
            }

            if (p.HP >= p.MaxHP)
            {
                await botClient.SendMessage(
                    chatId,
                    StoryService.Local(p.Lang,
                        "HP вже повне.",
                        "HP уже полное.",
                        "Your HP is already full."),
                    cancellationToken: ct);
                return;
            }

            int heal = 10;
            int before = p.HP;

            p.Potions -= 1;
            p.HP = Math.Min(p.MaxHP, p.HP + heal);

            session.UsedPotionThisCombat = true;
            if (session.RaidersEncounterActive)
                session.UsedPotionThisRaidersEncounter = true;

            await botClient.SendMessage(
                chatId,
                StoryService.Local(p.Lang,
                    $"🧪 +{p.HP - before} HP. Зілля залишилось: {p.Potions}.",
                    $"🧪 +{p.HP - before} HP. Хилок осталось: {p.Potions}.",
                    $"🧪 +{p.HP - before} HP. Potions left: {p.Potions}."),
                cancellationToken: ct);
        }

        private static void EndCombat(Session session)
        {
            var p = session.Player;
            p.InFight = false;
            p.CurrentEnemy = null;
            session.NextSceneAfterCombat = null;
        }

        private static string ResolveAfterCombatScene(Session session, bool playerWon)
        {
            bool usedPotion = session.CurrentCombatKind == EnemyKind.ForestRaider
                ? session.UsedPotionThisRaidersEncounter
                : session.UsedPotionThisCombat;

            if (!playerWon)
            {
                if (session.CurrentCombatKind == EnemyKind.GoblinSpearman)
                    return "SC03D";

                if (session.CurrentCombatKind == EnemyKind.ForestRaider && session.RaidersEncounterActive)
                {
                    if (session.RaidersDefeated <= 0)
                        return usedPotion ? "SC09_D0P" : "SC09_D0";

                    return usedPotion ? "SC09_D1P" : "SC09_D1";
                }

                if (session.CurrentCombatKind == EnemyKind.ForestLord)
                    return "SC12L";

                return "SC12L";
            }

            if (session.CurrentCombatKind == EnemyKind.GoblinSpearman)
                return usedPotion ? "SC03P" : "SC03";

            if (session.CurrentCombatKind == EnemyKind.ForestRaider && session.RaidersEncounterActive)
            {
                session.RaidersDefeated++;

                if (session.RaidersDefeated == 1)
                    return usedPotion ? "SC09_1P" : "SC09_1";

                session.RaidersEncounterActive = false;
                session.UsedPotionThisRaidersEncounter = false;
                return usedPotion ? "SC09_2P" : "SC09_2";
            }

            if (session.CurrentCombatKind == EnemyKind.ForestLord)
                return "SC12W";

            return StoryService.StartSceneId;
        }

        private static async Task SendSceneAsync(
            ITelegramBotClient botClient,
            long chatId,
            Session session,
            CancellationToken ct)
        {
            var scene = Story.GetScene(session.SceneId);

            await SendScenePhotoIfExistsAsync(botClient, chatId, session.SceneId, ct);

            string text = Story.RenderSceneText(session, scene);

            if (scene.Choices.Count == 0)
            {
                await botClient.SendMessage(chatId, text, cancellationToken: ct);
                return;
            }

            var kb = Story.BuildSceneKeyboard(session, scene);
            await botClient.SendMessage(chatId, text, replyMarkup: kb, cancellationToken: ct);
        }

        private static async Task SendScenePhotoIfExistsAsync(
            ITelegramBotClient botClient,
            long chatId,
            string sceneId,
            CancellationToken ct)
        {
            string? photoPath = FindScenePhotoPath(sceneId);

            if (string.IsNullOrWhiteSpace(photoPath) || !File.Exists(photoPath))
                return;

            try
            {
                await using var stream = File.OpenRead(photoPath);

                await botClient.SendPhoto(
                    chatId,
                    InputFile.FromStream(stream, Path.GetFileName(photoPath)),
                    cancellationToken: ct);
            }
            catch
            {
            }
        }

        private static string? FindScenePhotoPath(string sceneId)
        {
            var sceneToFileName = new Dictionary<string, string>
            {
                ["SC00"] = "0",
                ["SC01"] = "1",
                ["SC02"] = "2",

                ["SC03"] = "3",
                ["SC03P"] = "3.2",
                ["SC03D"] = "3.3",

                ["SC04"] = "4",
                ["SC05"] = "5",
                ["SC06"] = "6",

                ["SC07M"] = "7.1",
                ["SC07S"] = "7.2",
                ["SC07N"] = "7.3",

                ["SC08"] = "8",
                ["SC09"] = "9",

                ["SC09_1"] = "9.1",
                ["SC09_1P"] = "9.2",
                ["SC09_D0"] = "9.3",
                ["SC09_D0P"] = "9.4",
                ["SC09_2P"] = "9.5",
                ["SC09_2"] = "9.6",
                ["SC09_D1P"] = "9.7",
                ["SC09_D1"] = "9.8",

                ["SC10"] = "10",
                ["SC11"] = "11",
                ["SC12W"] = "12.1",
                ["SC12L"] = "12.2",
                ["SC13"] = "13"
            };

            if (!sceneToFileName.TryGetValue(sceneId, out var fileName))
                return null;

            string[] roots =
            {
                Path.Combine(Directory.GetCurrentDirectory(), "Assets", "Scenes"),
                Path.Combine(AppContext.BaseDirectory, "Assets", "Scenes")
            };

            string[] exts = { ".png", ".jpg", ".jpeg" };

            foreach (var root in roots)
            {
                foreach (var ext in exts)
                {
                    string path = Path.Combine(root, fileName + ext);
                    if (File.Exists(path))
                        return path;
                }
            }

            return null;
        }

        private static async Task SendShopAsync(
            ITelegramBotClient botClient,
            long chatId,
            Session session,
            CancellationToken ct)
        {
            var p = session.Player;

            string text = StoryService.Local(p.Lang,
                "🛒 Крамниця\n- /hud щоб глянути стан\n\nКнопки:\n1) Торгуватися (d6)\n2) Купити зілля\n3) Піти далі",
                "🛒 Магазин\n- /hud чтобы посмотреть статус\n\nКнопки:\n1) Поторговаться (d6)\n2) Купить хилку\n3) Уйти дальше",
                "🛒 Shop\n- /hud to see status\n\nButtons:\n1) Haggle (d6)\n2) Buy potion\n3) Leave");

            var kb = new InlineKeyboardMarkup(new[]
            {
                new [] { InlineKeyboardButton.WithCallbackData(StoryService.Local(p.Lang, "🎲 Торгуватися", "🎲 Торговаться", "🎲 Haggle"), "shop|haggle") },
                new [] { InlineKeyboardButton.WithCallbackData(StoryService.Local(p.Lang, "🧪 Купити зілля", "🧪 Купить хилку", "🧪 Buy potion"), "shop|buy_potion") },
                new [] { InlineKeyboardButton.WithCallbackData(StoryService.Local(p.Lang, "➡ Піти далі", "➡ Уйти дальше", "➡ Leave"), "shop|leave") }
            });

            await botClient.SendMessage(chatId, text, replyMarkup: kb, cancellationToken: ct);
        }

        private static async Task DoHaggleAsync(
            ITelegramBotClient botClient,
            long chatId,
            Session session,
            CancellationToken ct)
        {
            var p = session.Player;

            int d6 = Random.Shared.Next(1, 7);

            string msg = StoryService.Local(p.Lang,
                $"🎲 d6: {d6}\n",
                $"🎲 d6: {d6}\n",
                $"🎲 d6: {d6}\n");

            int discount = d6 switch
            {
                6 => 2,
                5 => 2,
                4 => 1,
                _ => 0
            };

            session.ShopDiscount = discount;

            msg += StoryService.Local(p.Lang,
                discount == 0 ? "Торгу не вийшло." : $"Знижка: -{discount} gold на зілля.",
                discount == 0 ? "Торг не удался." : $"Скидка: -{discount} gold на хилку.",
                discount == 0 ? "No luck haggling." : $"Discount: -{discount} gold on potions.");

            await botClient.SendMessage(chatId, msg, cancellationToken: ct);
            await SendShopAsync(botClient, chatId, session, ct);
        }

        private static async Task BuyPotionAsync(
            ITelegramBotClient botClient,
            long chatId,
            Session session,
            CancellationToken ct)
        {
            var p = session.Player;

            int basePrice = 6;
            int price = Math.Max(2, basePrice - session.ShopDiscount);

            if (p.Gold < price)
            {
                await botClient.SendMessage(
                    chatId,
                    StoryService.Local(p.Lang,
                        $"Не вистачає золота. Потрібно: {price}.",
                        $"Не хватает золота. Нужно: {price}.",
                        $"Not enough gold. Need: {price}."),
                    cancellationToken: ct);
                await SendShopAsync(botClient, chatId, session, ct);
                return;
            }

            p.Gold -= price;
            p.Potions += 1;

            await botClient.SendMessage(
                chatId,
                StoryService.Local(p.Lang,
                    $"Куплено зілля за {price} gold. Зілля: {p.Potions}.",
                    $"Куплена хилка за {price} gold. Хилки: {p.Potions}.",
                    $"Bought a potion for {price} gold. Potions: {p.Potions}."),
                cancellationToken: ct);

            await SendShopAsync(botClient, chatId, session, ct);
        }

        private static void ApplyRewards(Player p, Enemy enemy)
        {
            p.XP += enemy.XPReward;
            p.Gold += enemy.GoldReward;

            while (p.XP >= p.XPToNextLevel)
            {
                p.XP -= p.XPToNextLevel;
                p.Level += 1;
                p.XPToNextLevel += 10;
                p.MaxHP += 2;
                p.HP = p.MaxHP;
            }
        }

        private static string RewardText(Player p, Enemy enemy)
        {
            return StoryService.Local(p.Lang,
                $"🏆 Перемога! +{enemy.XPReward} XP, +{enemy.GoldReward} золота.",
                $"🏆 Победа! +{enemy.XPReward} XP, +{enemy.GoldReward} золота.",
                $"🏆 Victory! +{enemy.XPReward} XP, +{enemy.GoldReward} gold.");
        }

        private static string BuildHudText(Player p)
        {
            return StoryService.Local(p.Lang,
                $"📊 Статус\n❤️ HP: {p.HP}/{p.MaxHP}\n⭐ Рівень: {p.Level}\n🧪 Зілля: {p.Potions}\n💰 Золото: {p.Gold}\n✨ XP: {p.XP}/{p.XPToNextLevel}",
                $"📊 Статус\n❤️ HP: {p.HP}/{p.MaxHP}\n⭐ Уровень: {p.Level}\n🧪 Хилки: {p.Potions}\n💰 Золото: {p.Gold}\n✨ XP: {p.XP}/{p.XPToNextLevel}",
                $"📊 Status\n❤️ HP: {p.HP}/{p.MaxHP}\n⭐ Level: {p.Level}\n🧪 Potions: {p.Potions}\n💰 Gold: {p.Gold}\n✨ XP: {p.XP}/{p.XPToNextLevel}");
        }

        private static Task HandleErrorAsync(
            ITelegramBotClient botClient,
            Exception exception,
            CancellationToken cancellationToken)
        {
            Console.WriteLine(exception);
            return Task.CompletedTask;
        }

        private sealed class Session
        {
            public Player Player { get; set; } = new Player();

            public GameState State { get; set; } = GameState.MainMenu;
            public string SceneId { get; set; } = StoryService.StartSceneId;

            public Ally Ally { get; set; } = Ally.None;
            public int ShopDiscount { get; set; } = 0;

            public EnemyKind CurrentCombatKind { get; set; } = EnemyKind.GoblinSpearman;
            public bool UsedPotionThisCombat { get; set; }
            public bool PromptedPotionThisCombat { get; set; }

            public bool RaidersEncounterActive { get; set; }
            public int RaidersDefeated { get; set; }
            public bool UsedPotionThisRaidersEncounter { get; set; }

            public string? NextSceneAfterCombat { get; set; }
        }
    }
}
