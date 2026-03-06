using System;
using System.Collections.Generic;
using System.Reflection;
using LuckySvin_DD_bot.Game;
using LuckySvin_DD_bot.Models;
using Telegram.Bot.Types.ReplyMarkups;

namespace LuckySvin_DD_bot.Services
{
    public enum Ally
    {
        None,
        Merchant,
        Scout
    }

    public enum TransitionKind
    {
        None,
        GoToScene,
        StartCombat,
        OpenShop
    }

    public sealed class StoryTransition
    {
        public TransitionKind Kind { get; init; } = TransitionKind.None;
        public string? NextSceneId { get; init; }
        public EnemyKind? CombatEnemyKind { get; init; }
        public string? NextSceneAfterCombat { get; init; }

        public static StoryTransition None() => new() { Kind = TransitionKind.None };
        public static StoryTransition Go(string next) => new() { Kind = TransitionKind.GoToScene, NextSceneId = next };
        public static StoryTransition Combat(EnemyKind kind, string? after) => new()
        {
            Kind = TransitionKind.StartCombat,
            CombatEnemyKind = kind,
            NextSceneAfterCombat = after
        };
        public static StoryTransition Shop() => new() { Kind = TransitionKind.OpenShop };
    }

    public sealed class StoryChoice
    {
        public string Key { get; init; } = "";
        public string Ua { get; init; } = "";
        public string Ru { get; init; } = "";
        public string En { get; init; } = "";
    }

    public sealed class StoryScene
    {
        public string Id { get; init; } = "";
        public string Ua { get; init; } = "";
        public string Ru { get; init; } = "";
        public string En { get; init; } = "";
        public List<StoryChoice> Choices { get; init; } = new();
    }

    public sealed class StoryService
    {
        public const string StartSceneId = "SC00";

        // "AUTO_*" ids are resolved in Program.cs after combat ends.
        public const string AutoGoblin = "AUTO_GOBLIN";
        public const string AutoRaiders = "AUTO_RAIDERS";
        public const string AutoBoss = "AUTO_BOSS";

        private readonly Dictionary<string, StoryScene> _scenes = new();

        public StoryService()
        {
            // NOTE: Use dots / colons instead of long dashes.
            _scenes["SC00"] = new StoryScene
            {
                Id = "SC00",
                Ua = "0. Початок\n🐷 Ти - свин-воїн. Ти приходиш до тями у клітці. Десь поруч палає ферма, чути крики й тріск дерева.\n\nТи стискаєш руків'я меча та думаєш лише про одне: вибратися.",
                Ru = "0. Начало\n🐷 Ты - свин-воин. Ты приходишь в себя в клетке. Где-то рядом горит ферма, слышны крики и треск дерева.\n\nТы сжимаешь рукоять меча и думаешь только об одном: выбраться.",
                En = "0. Beginning\n🐷 You are a pig warrior. You wake up in a cage. Somewhere nearby the farm is burning, screams and cracking wood fill the air.\n\nYou grip your sword. Only one goal: get out.",
                Choices = new()
                {
                    new StoryChoice { Key = "next", Ua = "Далі", Ru = "Дальше", En = "Next" }
                }
            };

            _scenes["SC01"] = new StoryScene
            {
                Id = "SC01",
                Ua = "1. Грім і ворони\nНад тобою кружляють ворони. Десь у тумані гуркоче, наче важкі кроки.\n\nТи виходиш із клітки і бачиш, як щось дрібне шастає між уламків.",
                Ru = "1. Грохот и вороны\nНад тобой кружат вороны. Где-то в тумане грохочет, будто тяжелые шаги.\n\nТы выбираешься из клетки и видишь, как что-то мелкое шныряет среди обломков.",
                En = "1. Rumble and crows\nCrows circle above. In the fog you hear a heavy rumble, like slow footsteps.\n\nYou step out of the cage and spot something small darting between the debris.",
                Choices = new()
                {
                    new StoryChoice { Key = "next", Ua = "Підійти ближче", Ru = "Подойти ближе", En = "Get closer" }
                }
            };

            _scenes["SC02"] = new StoryScene
            {
                Id = "SC02",
                Ua = "2. Гоблін-воришка\nГоблін нишпорить біля тіла охоронця, тягне ключі й дрібні монети. Він помічає тебе, оскалюється і виставляє спис.\n\nКоманди бою: /attack, /potion",
                Ru = "2. Гоблин-воришка\nГоблин шарит у тела охранника, тащит ключи и мелочь. Он замечает тебя, скалится и выставляет копье.\n\nКоманды боя: /attack, /potion",
                En = "2. Goblin thief\nA goblin rummages through a guard's body, grabbing keys and coins. It notices you, grins, and levels a spear.\n\nCombat commands: /attack, /potion",
                Choices = new()
                {
                    new StoryChoice { Key = "fight", Ua = "Напасти", Ru = "Напасть", En = "Attack" }
                }
            };

            _scenes["SC03"] = new StoryScene
            {
                Id = "SC03",
                Ua = "3. Гоблін переможений (без зілля)\nГоблін падає, а ти підбираєш кілька монет і ключ. Дим згущається, час іти далі.",
                Ru = "3. Гоблин побежден (без хилки)\nГоблин падает, а ты подбираешь пару монет и ключ. Дым густеет, пора идти дальше.",
                En = "3. Goblin defeated (no potion)\nThe goblin drops. You pick up a few coins and a key. The smoke grows thicker - time to move on.",
                Choices = new()
                {
                    new StoryChoice { Key = "next", Ua = "Далі", Ru = "Дальше", En = "Next" }
                }
            };

            _scenes["SC03P"] = new StoryScene
            {
                Id = "SC03P",
                Ua = "3.2. Гоблін переможений (із зіллям)\nТи встигаєш випити зілля в бою й добиваєш гобліна. Серце ще стукає в скронях, але ти живий.",
                Ru = "3.2. Гоблин побежден (с хилкой)\nТы успеваешь выпить хилку в бою и добиваешь гоблина. Сердце стучит в висках, но ты жив.",
                En = "3.2. Goblin defeated (with potion)\nYou down a potion mid-fight and finish the goblin. Your pulse is loud in your ears - but you're alive.",
                Choices = new()
                {
                    new StoryChoice { Key = "next", Ua = "Далі", Ru = "Дальше", En = "Next" }
                }
            };

            _scenes["SC03D"] = new StoryScene
            {
                Id = "SC03D",
                Ua = "3.3. Герой загинув від гобліна-списника\nСпис знаходить щілину в броні. Ти падаєш на холодну землю.\n\nЩоб почати знову: /start",
                Ru = "3.3. Герой умер от гоблина с копьем\nКопье находит щель в броне. Ты падаешь на холодную землю.\n\nЧтобы начать заново: /start",
                En = "3.3. The hero dies to the goblin spearman\nThe spear slips past your guard. You collapse onto cold ground.\n\nTo restart: /start"
            };

            _scenes["SC04"] = new StoryScene
            {
                Id = "SC04",
                Ua = "4. Сліди рейдерів\nНа землі видно сліди чобіт і волочіння. Хтось тягнув щось важке вглиб лісу.\n\nТи йдеш за слідами, тримаючи меч напоготові.",
                Ru = "4. Следы рейдеров\nНа земле видны следы сапог и волочение. Кто-то тащил что-то тяжелое вглубь леса.\n\nТы идешь по следам, держа меч наготове.",
                En = "4. Raiders' tracks\nBootprints and drag marks cut through the mud. Someone pulled something heavy deeper into the forest.\n\nYou follow with your sword ready.",
                Choices = new()
                {
                    new StoryChoice { Key = "next", Ua = "Йти далі", Ru = "Идти дальше", En = "Continue" }
                }
            };

            _scenes["SC05"] = new StoryScene
            {
                Id = "SC05",
                Ua = "5. Місце падіння\nПопереду видно уламки воза. Кінь зник, а навколо - розірвані мотузки. Схоже, вантаж забрали силоміць.",
                Ru = "5. Место крушения\nВпереди обломки телеги. Лошадь пропала, вокруг - порванные веревки. Похоже, груз забрали силой.",
                En = "5. Crash site\nA shattered wagon lies ahead. The horse is gone, ropes are torn. Whatever was here, it was taken by force.",
                Choices = new()
                {
                    new StoryChoice { Key = "next", Ua = "Розбити табір", Ru = "Разбить лагерь", En = "Make camp" }
                }
            };

            _scenes["SC06"] = new StoryScene
            {
                Id = "SC06",
                Ua = "6. Тиха поляна\nНіч. Ти грієшся біля багаття. Ліс мовчить, тільки інколи шурхотить коріння.\n\nВранці ти вирішуєш: з ким іти далі?",
                Ru = "6. Тихая поляна\nНочь. Ты греешься у костра. Лес молчит, только иногда шуршит корнями.\n\nУтром ты решаешь: с кем идти дальше?",
                En = "6. Quiet glade\nNight. You warm your hands by the fire. The forest is silent, only roots rustle now and then.\n\nAt dawn you choose: who to trust on the road?",
                Choices = new()
                {
                    new StoryChoice { Key = "merchant", Ua = "7.1. Торговець", Ru = "7.1. Торговец", En = "7.1. Merchant" },
                    new StoryChoice { Key = "scout", Ua = "7.2. Розвідник", Ru = "7.2. Разведчик", En = "7.2. Scout" },
                    new StoryChoice { Key = "alone", Ua = "7.3. Сам", Ru = "7.3. Сам", En = "7.3. Go alone" }
                }
            };

            _scenes["SC07M"] = new StoryScene
            {
                Id = "SC07M",
                Ua = "7.1. Торговець\nТи знаходиш торговця під навісом з тканини. Він нервово посміхається і показує товар.\n\nТи можеш поторгуватись (d6) і купити зілля.",
                Ru = "7.1. Торговец\nТы находишь торговца под навесом из ткани. Он нервно улыбается и показывает товар.\n\nТы можешь поторговаться (d6) и купить хилки.",
                En = "7.1. Merchant\nYou find a merchant under a cloth awning. He smiles nervously and shows his goods.\n\nYou can haggle (d6) and buy potions.",
                Choices = new()
                {
                    new StoryChoice { Key = "open_shop", Ua = "Торгувати", Ru = "Торговать", En = "Trade" },
                    new StoryChoice { Key = "next", Ua = "Йти далі", Ru = "Идти дальше", En = "Move on" }
                }
            };

            _scenes["SC07S"] = new StoryScene
            {
                Id = "SC07S",
                Ua = "7.2. Розвідник\nРозвідник показує жести: обхідні стежки, кам'яний прохід і місце засідки рейдерів.\n\nТи запам'ятовуєш підказки.",
                Ru = "7.2. Разведчик\nРазведчик жестами показывает обходные тропы, каменный проход и место засады рейдеров.\n\nТы запоминаешь подсказки.",
                En = "7.2. Scout\nThe scout points out side paths, a stone passage, and where raiders like to ambush.\n\nYou commit the hints to memory.",
                Choices = new()
                {
                    new StoryChoice { Key = "next", Ua = "Йти далі", Ru = "Идти дальше", En = "Move on" }
                }
            };

            _scenes["SC07N"] = new StoryScene
            {
                Id = "SC07N",
                Ua = "7.3. Сам\nТи йдеш сам. У голові крутиться одна думка: якщо рейдери тут, то хтось керує ними.\n\nТи посилюєш хват меча і рухаєшся вперед.",
                Ru = "7.3. Сам\nТы идешь сам. В голове одна мысль: если рейдеры здесь, значит кто-то ими управляет.\n\nТы крепче сжимаешь меч и движешься вперед.",
                En = "7.3. Alone\nYou go alone. One thought repeats: if raiders are here, someone is guiding them.\n\nYou tighten your grip on the sword and press on.",
                Choices = new()
                {
                    new StoryChoice { Key = "next", Ua = "Йти далі", Ru = "Идти дальше", En = "Move on" }
                }
            };

            _scenes["SC08"] = new StoryScene
            {
                Id = "SC08",
                Ua = "8. Кам'яний прохід\nМіж скель утворився вузький прохід. Холодний камінь тисне з обох боків, а вдалині чути голоси.",
                Ru = "8. Каменный проход\nМежду скал узкий проход. Холодный камень давит с обеих сторон, а вдали слышны голоса.",
                En = "8. Stone passage\nA narrow passage cuts between rocks. Cold stone presses in on both sides, and distant voices echo ahead.",
                Choices = new()
                {
                    new StoryChoice { Key = "next", Ua = "Підійти тихо", Ru = "Подойти тихо", En = "Approach quietly" }
                }
            };

            _scenes["SC09"] = new StoryScene
            {
                Id = "SC09",
                Ua = "9. Рейдери\nТи бачиш загін рейдерів. Один стоїть ближче, двоє тримаються позаду.\n\nТут не буде переговорів.",
                Ru = "9. Рейдеры\nТы видишь отряд рейдеров. Один ближе, двое держатся позади.\n\nТут не будет переговоров.",
                En = "9. Raiders\nYou spot a raider squad. One is close, two hang back.\n\nThere will be no negotiation here.",
                Choices = new()
                {
                    new StoryChoice { Key = "fight", Ua = "Битися", Ru = "Драться", En = "Fight" }
                }
            };

            _scenes["SC09_1"] = new StoryScene
            {
                Id = "SC09_1",
                Ua = "9.1. Перемога над 1 рейдером (без зілля)\nПерший рейдер падає. Двоє інших озираються і роблять крок уперед.",
                Ru = "9.1. Побежден 1 рейдер (без хилки)\nПервый рейдер падает. Двое остальных переглядываются и делают шаг вперед.",
                En = "9.1. One raider defeated (no potion)\nThe first raider drops. The other two glance at each other and step forward.",
                Choices = new()
                {
                    new StoryChoice { Key = "continue", Ua = "Продовжити бій", Ru = "Продолжить бой", En = "Continue the fight" }
                }
            };

            _scenes["SC09_1P"] = new StoryScene
            {
                Id = "SC09_1P",
                Ua = "9.2. Перемога над 1 рейдером (із зіллям)\nТи вже витратив зілля, але вистояв і прибрав першого рейдера. Інші двоє йдуть на тебе.",
                Ru = "9.2. Побежден 1 рейдер (с хилкой)\nТы уже потратил хилку, но выстоял и убрал первого рейдера. Остальные двое идут на тебя.",
                En = "9.2. One raider defeated (with potion)\nYou already used a potion, but you stood your ground and took down the first raider. The other two come at you.",
                Choices = new()
                {
                    new StoryChoice { Key = "continue", Ua = "Продовжити бій", Ru = "Продолжить бой", En = "Continue the fight" }
                }
            };

            _scenes["SC09_D0"] = new StoryScene
            {
                Id = "SC09_D0",
                Ua = "9.3. Рейдери перемогли героя (без зілля)\nТебе валять на землю ще до того, як ти встигаєш завдати вирішального удару.\n\nЩоб почати знову: /start",
                Ru = "9.3. Рейдеры победили героя (без хилки)\nТебя валят на землю еще до того, как ты успеваешь нанести решающий удар.\n\nЧтобы начать заново: /start",
                En = "9.3. Raiders defeat the hero (no potion)\nThey bring you down before you can land a decisive blow.\n\nTo restart: /start"
            };

            _scenes["SC09_D0P"] = new StoryScene
            {
                Id = "SC09_D0P",
                Ua = "9.4. Рейдери перемогли героя (із зіллям)\nНавіть зілля не рятує. Ти відступаєш, але рейдери добивають тебе.\n\nЩоб почати знову: /start",
                Ru = "9.4. Рейдеры победили героя (с хилкой)\nДаже хилка не спасает. Ты отступаешь, но рейдеры добивают.\n\nЧтобы начать заново: /start",
                En = "9.4. Raiders defeat the hero (with potion)\nEven a potion isn't enough. You try to retreat, but they finish the job.\n\nTo restart: /start"
            };

            _scenes["SC09_2P"] = new StoryScene
            {
                Id = "SC09_2P",
                Ua = "9.5. Перемога над 2 рейдерами (із зіллям)\nДругий рейдер падає. Третій, що стояв далі, відступає і зникає в тумані.\n\nТи вижив, але розумієш: попереду щось більше.",
                Ru = "9.5. Побеждено 2 рейдера (с хилкой)\nВторой рейдер падает. Третий, который стоял вдалеке, отступает и исчезает в тумане.\n\nТы выжил, но понимаешь: впереди что-то большее.",
                En = "9.5. Two raiders defeated (with potion)\nThe second raider drops. The third, who stayed back, retreats and vanishes into the fog.\n\nYou survived, but you feel something bigger ahead.",
                Choices = new()
                {
                    new StoryChoice { Key = "next", Ua = "Далі", Ru = "Дальше", En = "Next" }
                }
            };

            _scenes["SC09_2"] = new StoryScene
            {
                Id = "SC09_2",
                Ua = "9.6. Перемога над 2 рейдерами (без зілля)\nТи прибираєш другого рейдера. Третій тікає в ліс, не ризикуючи битися далі.\n\nТиша повертається, але ненадовго.",
                Ru = "9.6. Побеждено 2 рейдера (без хилки)\nТы убираешь второго рейдера. Третий убегает в лес, не рискуя продолжать бой.\n\nТишина возвращается, но ненадолго.",
                En = "9.6. Two raiders defeated (no potion)\nYou take down the second raider. The third runs into the woods, unwilling to keep fighting.\n\nSilence returns - briefly.",
                Choices = new()
                {
                    new StoryChoice { Key = "next", Ua = "Далі", Ru = "Дальше", En = "Next" }
                }
            };

            _scenes["SC09_D1P"] = new StoryScene
            {
                Id = "SC09_D1P",
                Ua = "9.7. Герой загинув, але 1 ворог був переможений (із зіллям)\nТи встигаєш покласти одного рейдера, але сили зникають. Інші добивають тебе.\n\nЩоб почати знову: /start",
                Ru = "9.7. Герой погиб, но 1 враг был побежден (с хилкой)\nТы успеваешь положить одного рейдера, но силы заканчиваются. Остальные добивают тебя.\n\nЧтобы начать заново: /start",
                En = "9.7. Hero dies, but one enemy fell (with potion)\nYou drop one raider, but your strength fades. The others finish you.\n\nTo restart: /start"
            };

            _scenes["SC09_D1"] = new StoryScene
            {
                Id = "SC09_D1",
                Ua = "9.8. Герой загинув, але 1 ворог був переможений (без зілля)\nТи збиваєш одного рейдера, та цього замало. Удар ззаду і темрява.\n\nЩоб почати знову: /start",
                Ru = "9.8. Герой погиб, но 1 враг был побежден (без хилки)\nТы снимаешь одного рейдера, но этого мало. Удар со спины и темнота.\n\nЧтобы начать заново: /start",
                En = "9.8. Hero dies, but one enemy fell (no potion)\nYou take one raider down, but it's not enough. A hit from behind - and darkness.\n\nTo restart: /start"
            };

            _scenes["SC10"] = new StoryScene
            {
                Id = "SC10",
                Ua = "10. Шепчущі корені\nКоріння під ногами ворушиться. Здається, ліс шепоче твоє ім'я. Попереду темніє, наче брама.",
                Ru = "10. Шепчущие корни\nКорни под ногами шевелятся. Кажется, лес шепчет твое имя. Впереди темнеет, как будто ворота.",
                En = "10. Whispering roots\nRoots stir underfoot. The forest seems to whisper your name. Ahead, darkness gathers like a gate.",
                Choices = new()
                {
                    new StoryChoice { Key = "next", Ua = "Зайти всередину", Ru = "Войти внутрь", En = "Step inside" }
                }
            };

            _scenes["SC11"] = new StoryScene
            {
                Id = "SC11",
                Ua = "11. Бос: Лісовий Лорд\nГілки змикаються, і з тіні виходить велетенська постать. Це Лісовий Лорд.\n\nКоманди бою: /attack, /potion",
                Ru = "11. Босс: Лесной Лорд\nВетки смыкаются, и из тени выходит огромная фигура. Это Лесной Лорд.\n\nКоманды боя: /attack, /potion",
                En = "11. Boss: Forest Lord\nBranches close in, and a massive silhouette steps out of the shadow. The Forest Lord.\n\nCombat commands: /attack, /potion",
                Choices = new()
                {
                    new StoryChoice { Key = "fight", Ua = "Битися", Ru = "Драться", En = "Fight" }
                }
            };

            _scenes["SC12W"] = new StoryScene
            {
                Id = "SC12W",
                Ua = "12.1. Бос переможений\nЛісовий Лорд падає. Коріння стихає, а повітря стає легшим.\n\nТи знайшов вихід.",
                Ru = "12.1. Босс побежден\nЛесной Лорд падает. Корни затихают, воздух становится легче.\n\nТы находишь выход.",
                En = "12.1. Boss defeated\nThe Forest Lord collapses. The roots fall silent, and the air feels lighter.\n\nYou found the way out.",
                Choices = new()
                {
                    new StoryChoice { Key = "next", Ua = "Далі", Ru = "Дальше", En = "Next" }
                }
            };

            _scenes["SC12L"] = new StoryScene
            {
                Id = "SC12L",
                Ua = "12.2. Бос переміг\nКоріння обвиває тебе, і світ гасне. Ліс забирає своє.\n\nЩоб почати знову: /start",
                Ru = "12.2. Босс победил\nКорни обвивают тебя, и мир гаснет. Лес забирает свое.\n\nЧтобы начать заново: /start",
                En = "12.2. Boss wins\nRoots coil around you and the world goes dark. The forest takes what it wants.\n\nTo restart: /start"
            };

            _scenes["SC13"] = new StoryScene
            {
                Id = "SC13",
                Ua = "13. Вихід з лісу\nТи виходиш із лісу. Позаду - шепіт, попереду - невідомість.\n\nПродовження буде...",
                Ru = "13. Свин вышел из леса\nТы выходишь из леса. Позади шепот, впереди неизвестность.\n\nПродолжение следует...",
                En = "13. Out of the forest\nYou step out of the forest. Behind you: whispers. Ahead: the unknown.\n\nTo be continued..."
            };
        }

        public StoryScene GetScene(string id) => _scenes[id];

        public string RenderSceneText(object session, StoryScene scene)
        {
            var p = GetProp<Player>(session, "Player")!;
            return Local(p.Lang, scene.Ua, scene.Ru, scene.En);
        }

        public InlineKeyboardMarkup BuildSceneKeyboard(object session, StoryScene scene)
        {
            var p = GetProp<Player>(session, "Player")!;

            if (scene.Choices.Count == 0)
                return new InlineKeyboardMarkup(Array.Empty<InlineKeyboardButton[]>());

            var rows = new List<InlineKeyboardButton[]>();
            foreach (var c in scene.Choices)
            {
                string text = Local(p.Lang, c.Ua, c.Ru, c.En);
                rows.Add(new[]
                {
                    InlineKeyboardButton.WithCallbackData(text, $"ch|{scene.Id}|{c.Key}")
                });
            }

            return new InlineKeyboardMarkup(rows);
        }

        public StoryTransition ApplyChoice(object session, string sceneId, string choiceKey)
        {
            // Helper setters via reflection (Session is private in Program.cs).
            void Set<T>(string prop, T value)
            {
                var pi = session.GetType().GetProperty(prop, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (pi != null && pi.CanWrite) pi.SetValue(session, value);
            }

            switch (sceneId)
            {
                case "SC00":
                    return choiceKey == "next" ? StoryTransition.Go("SC01") : StoryTransition.None();

                case "SC01":
                    return choiceKey == "next" ? StoryTransition.Go("SC02") : StoryTransition.None();

                case "SC02":
                    if (choiceKey == "fight")
                        return StoryTransition.Combat(EnemyKind.GoblinSpearman, AutoGoblin);
                    return StoryTransition.None();

                case "SC03":
                case "SC03P":
                    return choiceKey == "next" ? StoryTransition.Go("SC04") : StoryTransition.None();

                case "SC04":
                    return choiceKey == "next" ? StoryTransition.Go("SC05") : StoryTransition.None();

                case "SC05":
                    return choiceKey == "next" ? StoryTransition.Go("SC06") : StoryTransition.None();

                case "SC06":
                    switch (choiceKey)
                    {
                        case "merchant":
                            Set("Ally", Ally.Merchant);
                            return StoryTransition.Go("SC07M");
                        case "scout":
                            Set("Ally", Ally.Scout);
                            return StoryTransition.Go("SC07S");
                        case "alone":
                            Set("Ally", Ally.None);
                            return StoryTransition.Go("SC07N");
                    }
                    return StoryTransition.None();

                case "SC07M":
                    if (choiceKey == "open_shop") return StoryTransition.Shop();
                    if (choiceKey == "next") return StoryTransition.Go("SC08");
                    return StoryTransition.None();

                case "SC07S":
                case "SC07N":
                    return choiceKey == "next" ? StoryTransition.Go("SC08") : StoryTransition.None();

                case "SC08":
                    return choiceKey == "next" ? StoryTransition.Go("SC09") : StoryTransition.None();

                case "SC09":
                    if (choiceKey == "fight")
                    {
                        Set("RaidersEncounterActive", true);
                        Set("RaidersDefeated", 0);
                        return StoryTransition.Combat(EnemyKind.ForestRaider, AutoRaiders);
                    }
                    return StoryTransition.None();

                case "SC09_1":
                case "SC09_1P":
                    if (choiceKey == "continue")
                        return StoryTransition.Combat(EnemyKind.ForestRaider, AutoRaiders);
                    return StoryTransition.None();

                case "SC09_2":
                case "SC09_2P":
                    return choiceKey == "next" ? StoryTransition.Go("SC10") : StoryTransition.None();

                case "SC10":
                    return choiceKey == "next" ? StoryTransition.Go("SC11") : StoryTransition.None();

                case "SC11":
                    if (choiceKey == "fight")
                        return StoryTransition.Combat(EnemyKind.ForestLord, AutoBoss);
                    return StoryTransition.None();

                case "SC12W":
                    return choiceKey == "next" ? StoryTransition.Go("SC13") : StoryTransition.None();
            }

            return StoryTransition.None();
        }

        public static string Local(Language lang, string ua, string ru, string en)
        {
            return lang switch
            {
                Language.UA => ua,
                Language.RU => ru,
                _ => en
            };
        }

        private static T? GetProp<T>(object obj, string name) where T : class
        {
            var pi = obj.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return pi?.GetValue(obj) as T;
        }
    }
}
