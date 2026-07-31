using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RunGun.Settings
{
    public enum GameLanguage
    {
        English,
        Russian
    }

    public static class GameLocalization
    {
        private const string LanguageKey = "RunGun.Settings.Language";
        private const string EnglishTablePath = "Localization/EnglishLocalization";
        private const string RussianTablePath = "Localization/RussianLocalization";
        private static readonly Dictionary<TMP_Text, string> TmpSources = new();
        private static readonly Dictionary<Text, string> LegacySources = new();
        private static readonly Dictionary<TMP_Text, TmpAutoSizeDefaults> TmpAutoSizeSettings = new();
        private static readonly Dictionary<TMP_Text, TMP_FontAsset> TmpFonts = new();
        private static LocalizationTable _englishTable;
        private static LocalizationTable _russianTable;
        private static TMP_FontAsset _cyrillicFont;

        private readonly struct TmpAutoSizeDefaults
        {
            public readonly bool Enabled;
            public readonly float Min;
            public readonly float Max;

            public TmpAutoSizeDefaults(TMP_Text text)
            {
                Enabled = text.enableAutoSizing;
                Min = text.fontSizeMin;
                Max = text.fontSizeMax;

                if (!Enabled)
                {
                    Min = Mathf.Min(1f, text.fontSize);
                    Max = text.fontSize;
                }
            }
        }

        private static readonly Dictionary<string, string> RussianDefaults = new(StringComparer.Ordinal)
        {
            ["SETTINGS"] = "НАСТРОЙКИ",
            ["DISPLAY"] = "ЭКРАН",
            ["Display"] = "ЭКРАН",
            ["AUDIO"] = "АУДИО",
            ["CONTROLS"] = "УПРАВЛЕНИЕ",
            ["CROSSHAIR"] = "ПРИЦЕЛ",
            ["LANGUAGE"] = "ЯЗЫК",
            ["RESOLUTION"] = "РАЗРЕШЕНИЕ",
            ["WINDOW MODE"] = "РЕЖИМ ЭКРАНА",
            ["Borderless"] = "Без рамки",
            ["Exclusive Fullscreen"] = "Полный экран",
            ["Windowed"] = "В окне",
            ["MUSIC VOLUME"] = "ГРОМКОСТЬ МУЗЫКИ",
            ["SFX VOLUME"] = "ГРОМКОСТЬ ЭФФЕКТОВ",
            ["MOUSE SENSITIVITY"] = "ЧУВСТВИТЕЛЬНОСТЬ МЫШИ",
            ["KEY BINDINGS"] = "НАЗНАЧЕНИЕ КЛАВИШ",
            ["MOVE FORWARD"] = "ДВИЖЕНИЕ ВПЕРЁД",
            ["MOVE BACKWARD"] = "ДВИЖЕНИЕ НАЗАД",
            ["MOVE LEFT"] = "ДВИЖЕНИЕ ВЛЕВО",
            ["MOVE RIGHT"] = "ДВИЖЕНИЕ ВПРАВО",
            ["JUMP"] = "ПРЫЖОК",
            ["SPRINT"] = "БЕГ",
            ["CROUCH / SLIDE"] = "ПРИСЕСТЬ / СКОЛЬЖЕНИЕ",
            ["RELOAD"] = "ПЕРЕЗАРЯДКА",
            ["RESET TO DEFAULT"] = "СБРОСИТЬ НАСТРОЙКИ",
            ["APPEARANCE"] = "ВНЕШНИЙ ВИД",
            ["STYLE"] = "СТИЛЬ",
            ["COLOR"] = "ЦВЕТ",
            ["SIZE"] = "РАЗМЕР",
            ["OPACITY"] = "ПРОЗРАЧНОСТЬ",
            ["LIVE PREVIEW"] = "ПРЕДПРОСМОТР",
            ["DOT"] = "ТОЧКА",
            ["CLASSIC"] = "КЛАССИЧЕСКИЙ",
            ["CIRCLE"] = "КРУГ",
            ["CROSS"] = "КРЕСТ",
            ["WHITE"] = "БЕЛЫЙ",
            ["RED"] = "КРАСНЫЙ",
            ["CYAN"] = "ГОЛУБОЙ",
            ["GREEN"] = "ЗЕЛЁНЫЙ",
            ["YELLOW"] = "ЖЁЛТЫЙ",
            ["MAGENTA"] = "ПУРПУРНЫЙ",
            ["CHANGES ARE APPLIED IMMEDIATELY"] = "ИЗМЕНЕНИЯ ПРИМЕНЯЮТСЯ СРАЗУ",
            ["CHANGES ARE SAVED AUTOMATICALLY"] = "ИЗМЕНЕНИЯ СОХРАНЯЮТСЯ АВТОМАТИЧЕСКИ",
            ["RIGHT NOW ONLY 1920x1080 resolution is supported. later it will be chabged"] =
                "СЕЙЧАС ПОДДЕРЖИВАЕТСЯ ТОЛЬКО РАЗРЕШЕНИЕ 1920x1080",
            ["This is an early development build. Gameplay, levels, models, textures, audio, and balancing are still works in progress. Expect bugs, unfinished content, and significant changes in future versions. Thank you for playing!"] =
                "Это ранняя версия игры. Игровой процесс, уровни, модели, текстуры, звук и баланс всё ещё находятся в разработке. Возможны ошибки, незавершённый контент и значительные изменения в будущих версиях. Спасибо за игру!",
            ["LEVEL SELECT"] = "ВЫБОР УРОВНЯ",
            ["TRIALS"] = "ИСПЫТАНИЯ",
            ["LEVELS"] = "УРОВНИ",
            ["Trials"] = "Испытания",
            ["Levels"] = "Уровни",
            ["COMING SOON"] = "СКОРО",
            ["START"] = "СТАРТ",
            ["FINAL TEST"] = "ФИНАЛЬНЫЙ ТЕСТ",
            ["LICENSE TEST"] = "ЭКЗАМЕН НА ЛИЦЕНЗИЮ",
            ["MOVEMENT"] = "ДВИЖЕНИЕ",
            ["Trial 1 Move"] = "Испытание 1 - Движение",
            ["Trial 2 - Shoot"] = "Испытание 2 - Стрельба",
            ["Trial 3 - Shotgun"] = "Испытание 3 - Дробовик",
            ["Trial 4 - Rifle & Slide"] = "Испытание 4 - Винтовка и скольжение",
            ["Trial 5 - Heavy Lift"] = "Испытание 5 - Тяжёлый калибр",
            ["Final Trial"] = "Финальное испытание",
            ["EXIT"] = "ВЫХОД",
            ["PAUSED"] = "ПАУЗА",
            ["Continue"] = "Продолжить",
            ["Retry"] = "Повторить",
            ["Menu"] = "Меню",
            ["TRIAL COMPLETED"] = "ИСПЫТАНИЕ ПРОЙДЕНО",
            ["COMPLETE"] = "ЗАВЕРШЕНО",
            ["YOUR TIME"] = "ВАШЕ ВРЕМЯ",
            ["BEST TIME"] = "ЛУЧШЕЕ ВРЕМЯ",
            ["NEW RECORD"] = "НОВЫЙ РЕКОРД",
            ["RANK REQUIREMENTS"] = "ТРЕБОВАНИЯ К РАНГУ",
            ["IMPOSSIBLE"] = "НЕВОЗМОЖНО",
            ["GOLD"] = "ЗОЛОТО",
            ["SILVER"] = "СЕРЕБРО",
            ["BRONZE"] = "БРОНЗА",
            ["Impossible"] = "Невозможно",
            ["Gold"] = "Золото",
            ["Silver"] = "Серебро",
            ["Bronze"] = "Бронза",
            ["press Enter"] = "нажмите Enter",

            ["Movement"] = "Движение",
            ["Targets"] = "Мишени",
            ["Shotgun"] = "Дробовик",
            ["Rifle + Slide"] = "Винтовка + скольжение",
            ["Bazooka"] = "Базука",
            ["License Test"] = "Экзамен на лицензию",
            ["Begin Corps training with movement, jumping, and checkpoints."] =
                "Начните подготовку в Корпусе: движение, прыжки и контрольные точки.",
            ["Qualify with the pistol and clear moving training targets."] =
                "Пройдите подготовку с пистолетом и поразите движущиеся мишени.",
            ["Turn shotgun recoil into a movement tool."] =
                "Используйте отдачу дробовика для перемещения.",
            ["Combine rifle control, sliding, and courier momentum."] =
                "Совместите стрельбу из винтовки, скольжение и сохранение скорости.",
            ["Breach barriers and redirect yourself with explosive force."] =
                "Пробивайте преграды и меняйте траекторию силой взрыва.",
            ["Pass the timed exam and earn your Space Delivery Corps license."] =
                "Пройдите экзамен на время и получите лицензию Космического корпуса доставки.",

            ["Blue Man"] = "Инструктор",
            ["Blue Guy"] = "Инструктор",
            ["Welcome, recruit. You are training to join the Space Delivery Corps."] =
                "Добро пожаловать, рекрут. Вы проходите подготовку для вступления в Космический корпус доставки.",
            ["Couriers cross dangerous places where normal deliveries cannot go. Start with the basics. Use WASD to move and SPACE to jump."] =
                "Курьеры работают там, куда обычная доставка не доберётся. Начнём с основ: WASD - движение, SPACE - прыжок.",
            ["This course hangs in open space, but the training recovery system is active. If you fall, it will return you to your latest checkpoint."] =
                "Полигон находится в открытом космосе, но система восстановления активна. При падении вы вернётесь к последней контрольной точке.",
            ["Checkpoint confirmed. Your training record has been updated."] =
                "Контрольная точка подтверждена. Данные тренировки обновлены.",
            ["Next is sprinting. Hold SHIFT while moving to build the speed a field courier needs."] =
                "Далее - бег. Удерживайте SHIFT во время движения, чтобы набрать необходимую курьеру скорость.",
            ["Use a sprint jump to clear the gap ahead. Trust the recovery system if you miss."] =
                "Разбегитесь и перепрыгните разрыв. Если сорвётесь, система восстановления поможет.",
            ["Good work, recruit. One movement section remains."] =
                "Хорошая работа, рекрут. Осталась последняя секция движения.",
            ["Cross the three platforms ahead and reach the trial exit."] =
                "Пересеките три платформы и доберитесь до выхода.",
            ["Complete this course and report to weapons training. A licensed courier must be ready for hostile delivery zones."] =
                "Завершите курс и отправляйтесь на оружейную подготовку. Лицензированный курьер должен быть готов к опасным зонам доставки.",
            ["Welcome to weapons training. Your first courier sidearm is the pistol."] =
                "Добро пожаловать на оружейную подготовку. Ваше первое оружие курьера - пистолет.",
            ["Shoot all six targets to verify your aim and activate the next platform."] =
                "Поразите все шесть мишеней, чтобы подтвердить точность и активировать следующую платформу.",
            ["Static target calibration complete. Now prove you can aim under pressure."] =
                "Калибровка по неподвижным мишеням завершена. Теперь стреляйте под давлением.",
            ["Land on the platform and shoot all three moving targets before they reach the end. If one escapes, the training sequence resets."] =
                "Приземлитесь на платформу и поразите три движущиеся мишени. Если одна уйдёт, последовательность начнётся заново.",
            ["Pass this section and the next course will teach you how weapons can control your movement."] =
                "Пройдите секцию, и следующий курс научит управлять движением при помощи оружия.",
            ["In real delivery zones, your weapon is more than protection. Recoil can become a movement tool."] =
                "В настоящих зонах доставки оружие служит не только защитой. Отдачу можно использовать для движения.",
            ["Every weapon applies a different amount of force. Learning that force can keep a courier alive."] =
                "Каждое оружие создаёт разную силу отдачи. Знание этой силы может спасти курьеру жизнь.",
            ["This course uses the shotgun. Jump and fire at the ground beneath you to boost upward."] =
                "В этом курсе используется дробовик. Прыгните и выстрелите в землю под собой, чтобы взлететь выше.",
            ["Remember this before you enter a live delivery zone."] =
                "Запомните это перед выходом в настоящую зону доставки.",
            ["The shotgun gives you a stronger push when you fire at a nearby surface than when you fire into open space."] =
                "Дробовик толкает сильнее при выстреле в близкую поверхность, чем при выстреле в открытое пространство.",
            ["Use that extra force to reach the next platform."] =
                "Используйте дополнительную силу, чтобы добраться до следующей платформы.",
            ["Good. Now combine weapon handling with movement."] =
                "Хорошо. Теперь совместите обращение с оружием и движение.",
            ["Use the pistol to clear the targets and activate the platform."] =
                "Поразите мишени из пистолета и активируйте платформу.",
            ["Then use the shotgun to reach the finish. Face away from it, move BACKWARD, and fire to push yourself toward the exit."] =
                "Затем доберитесь до финиша с помощью дробовика. Повернитесь спиной, двигайтесь НАЗАД и выстрелите, чтобы толкнуть себя к выходу.",
            ["This trial adds the rifle, the Corps standard for sustained fire. Keep moving while you clear groups of targets."] =
                "В этом испытании используется винтовка - стандарт Корпуса для непрерывного огня. Не останавливайтесь, поражая группы мишеней.",
            ["Its recoil gives only a small movement push. Its strength is clearing several threats without breaking your flow."] =
                "Её отдача почти не толкает. Главное преимущество - уничтожение нескольких угроз без потери темпа.",
            ["Clear the seven targets ahead to open the first gate."] =
                "Поразите семь мишеней впереди, чтобы открыть первые ворота.",
            ["Fast deliveries demand control in tight spaces. Hold LEFT CTRL while moving downhill to begin a slide."] =
                "Быстрая доставка требует контроля в тесных местах. Удерживайте LEFT CTRL при спуске, чтобы начать скольжение.",
            ["Stay low beneath the gate, then jump from the runway to carry your speed across the gap."] =
                "Проскользните под воротами, затем прыгните с разгона, сохранив скорость над разрывом.",
            ["Now combine both lessons. Shoot every moving target while sliding through the final lane."] =
                "Теперь объедините оба навыка. Поразите все движущиеся мишени, скользя по финальной полосе.",
            ["The finish gate opens when the last target falls. Reach it without losing your courier momentum."] =
                "Финишные ворота откроются после последней мишени. Доберитесь до них, не теряя скорость.",
            ["Heavy cargo routes may require the bazooka. It carries one rocket, but no other courier weapon can match its impact."] =
                "На тяжёлых маршрутах может понадобиться базука. В ней одна ракета, но ни одно оружие курьера не сравнится с её мощью.",
            ["Fire at the reinforced red wall ahead. Only a heavy impact can breach this training barrier."] =
                "Выстрелите в укреплённую красную стену. Только мощный удар пробьёт этот учебный барьер.",
            ["Rockets take time to travel. Start reloading while you continue forward."] =
                "Ракете нужно время на полёт. Начните перезарядку, продолжая движение.",
            ["Now use the blast for movement. Jump, look down and slightly behind you, then fire at the orange training plate."] =
                "Теперь используйте взрыв для движения. Прыгните, посмотрите вниз и немного назад, затем выстрелите в оранжевую пластину.",
            ["The blast pushes you away from its impact point. Hold forward in the air and land on the platform above."] =
                "Взрыв отталкивает от точки попадания. Удерживайте движение вперёд в воздухе и приземлитесь на верхнюю платформу.",
            ["A trained Corps courier can also use a bazooka to redirect in mid-air."] =
                "Подготовленный курьер Корпуса может менять направление в воздухе с помощью базуки.",
            ["Jump forward and fire at the orange plate on the left wall. Its blast will push you right toward the next platform."] =
                "Прыгните вперёд и выстрелите в оранжевую пластину на левой стене. Взрыв отбросит вас вправо к платформе.",
            ["Final sequence. Breach the reinforced wall and reload while running."] =
                "Финальная последовательность. Пробейте укреплённую стену и перезарядитесь на бегу.",
            ["At the edge, jump and fire at the orange plate behind you. Ride the blast toward the finish."] =
                "У края прыгните и выстрелите в оранжевую пластину позади. Используйте взрыв, чтобы долететь до финиша.",
            ["Recruit, this is your Space Delivery Corps licensing exam. Cross the start line to begin the timer, then use everything you learned to reach the finish. Pass this test and your courier license will unlock real delivery assignments. Training recovery remains active here; outside this facility, it will not save you."] =
                "Рекрут, это экзамен на лицензию Космического корпуса доставки. Пересеките стартовую линию и примените все навыки, чтобы добраться до финиша. Успех откроет реальные задания. Здесь система восстановления ещё активна; за пределами полигона она вас не спасёт."
        };

        private static readonly Dictionary<string, string> EnglishDialogueTone = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Welcome, recruit. You are training to join the Space Delivery Corps."] = "Welcome aboard, rookie! Survive training and you might just earn a place in the Space Delivery Corps.",
            ["Couriers cross dangerous places where normal deliveries cannot go. Start with the basics. Use WASD to move and SPACE to jump."] = "We deliver where sane people won't. Let's start easy: WASD to move, SPACE to jump.",
            ["This course hangs in open space, but the training recovery system is active. If you fall, it will return you to your latest checkpoint."] = "And yes, that's open space below you. Fall off and recovery will pull you back to the last checkpoint. Probably with your dignity intact.",
            ["Checkpoint confirmed. Your training record has been updated."] = "Checkpoint locked! If space eats you, you're coming back here.",
            ["Next is sprinting. Hold SHIFT while moving to build the speed a field courier needs."] = "Walking is cute. Couriers run. Move and hold SHIFT to pick up speed.",
            ["Use a sprint jump to clear the gap ahead. Trust the recovery system if you miss."] = "Sprint, jump, clear the gap. Miss it? Recovery gets to laugh first.",
            ["Good work, recruit. One movement section remains."] = "Not bad, rookie. One last movement section.",
            ["Cross the three platforms ahead and reach the trial exit."] = "Three platforms, one exit. Keep moving and try not to admire the void.",
            ["Complete this course and report to weapons training. A licensed courier must be ready for hostile delivery zones."] = "Reach the exit and report to weapons training. Out there, some customers shoot before signing for the package.",
            ["Welcome to weapons training. Your first courier sidearm is the pistol."] = "Welcome to weapons training. Here's your pistol - the Corps' answer to rude customers.",
            ["Shoot all six targets to verify your aim and activate the next platform."] = "Drop all six targets and the next platform is yours. Nice and simple.",
            ["Static target calibration complete. Now prove you can aim under pressure."] = "Stationary targets are easy. Let's see how you handle something that moves.",
            ["Land on the platform and shoot all three moving targets before they reach the end. If one escapes, the training sequence resets."] = "Land on the platform and hit all three movers before they escape. Lose one and we reset the whole show.",
            ["Pass this section and the next course will teach you how weapons can control your movement."] = "Clear this and I'll show you the fun part: using guns to move yourself.",
            ["In real delivery zones, your weapon is more than protection. Recoil can become a movement tool."] = "Out there, a gun isn't just protection. Point it the right way and recoil becomes an engine.",
            ["Every weapon applies a different amount of force. Learning that force can keep a courier alive."] = "Every gun kicks differently. Learn the kick and it might save your delivery - and your skin.",
            ["This course uses the shotgun. Jump and fire at the ground beneath you to boost upward."] = "Grab the shotgun. Jump, fire at the ground, and let recoil introduce you to flight.",
            ["Remember this before you enter a live delivery zone."] = "Remember this trick. Real delivery zones won't give you a practice run.",
            ["The shotgun gives you a stronger push when you fire at a nearby surface than when you fire into open space."] = "The closer the surface, the harder the shotgun throws you. Physics is finally being helpful.",
            ["Use that extra force to reach the next platform."] = "Use that kick and launch yourself to the next platform.",
            ["Good. Now combine weapon handling with movement."] = "Good. Now let's mix shooting and movement - try to keep all limbs attached.",
            ["Use the pistol to clear the targets and activate the platform."] = "Clear the targets with the pistol and wake up that platform.",
            ["Then use the shotgun to reach the finish. Face away from it, move BACKWARD, and fire to push yourself toward the exit."] = "Then switch to the shotgun. Face away from the exit, move BACKWARD, fire, and ride the kick to the finish.",
            ["This trial adds the rifle, the Corps standard for sustained fire. Keep moving while you clear groups of targets."] = "Meet the rifle: loud, fast, and very good at clearing a route. Keep moving while you work.",
            ["Its recoil gives only a small movement push. Its strength is clearing several threats without breaking your flow."] = "It won't launch you far, but it shreds targets without killing your momentum.",
            ["Clear the seven targets ahead to open the first gate."] = "Seven targets between you and the gate. Make them disappear.",
            ["Fast deliveries demand control in tight spaces. Hold LEFT CTRL while moving downhill to begin a slide."] = "Tight route ahead. Move downhill and hold LEFT CTRL to slide under the trouble.",
            ["Stay low beneath the gate, then jump from the runway to carry your speed across the gap."] = "Stay low under the gate, then jump from the runway and carry that speed across.",
            ["Stay low, then jump from the runway to carry your speed across the gap and jump again in timing to keep momentum."] = "Stay low, launch from the runway, then time the next jump and keep that speed alive.",
            ["Now combine both lessons. Shoot every moving target while sliding through the final lane."] = "Final lane: slide fast, shoot faster, and don't let a single target escape.",
            ["The finish gate opens when the last target falls. Reach it without losing your courier momentum."] = "The last target opens the finish. Keep your speed and punch through.",
            ["Heavy cargo routes may require the bazooka. It carries one rocket, but no other courier weapon can match its impact."] = "Heavy route, heavy answer. The bazooka holds one rocket, but one is usually plenty.",
            ["Fire at the reinforced grid ahead. Only a heavy impact can breach this training barrier."] = "See that reinforced grid? Introduce it to the rocket.",
            ["Rockets take time to travel. Start reloading while you continue forward."] = "Rockets aren't instant. Fire, reload on the move, and don't wait around for the fireworks.",
            ["Now use the blast for movement. Jump, look down and slightly behind you, then fire."] = "Now ride the blast. Jump, look down and a little behind, then fire.",
            ["The blast pushes you away from its impact point. Hold forward in the air and land on the platform above."] = "The blast throws you away from the impact. Hold forward in the air and stick the landing above.",
            ["A trained Corps courier can also use a bazooka to redirect in mid-air."] = "A good courier can even change direction mid-air. A rocket helps.",
            ["Jump forward and fire at the platform on the left. Its blast will push you right toward the next platform."] = "Jump forward, fire at the left platform, and let the blast slap you right.",
            ["Final sequence. Breach the reinforced grid and reload while running."] = "Final run: break the grid and reload without slowing down.",
            ["At the edge, jump and fire at the platform behind you. Ride the blast toward the finish."] = "At the edge, jump and fire behind you. Then ride the explosion home.",
            ["Recruit, this is your Space Delivery Corps licensing exam. Cross the start line to begin the timer, then use everything you learned to reach the finish. Pass this test and your courier license will unlock real delivery assignments. Training recovery remains active here; outside this facility, it will not save you."] = "This is it, rookie - your licensing run. Cross the line, beat the course, and earn real assignments. Recovery still works here. Out there, nobody pulls you back."
        };

        private static readonly Dictionary<string, string> RussianDialogueTone = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Welcome, recruit. You are training to join the Space Delivery Corps."] = "Добро пожаловать, новичок! Переживёшь тренировку - получишь шанс вступить в Космический корпус доставки.",
            ["Couriers cross dangerous places where normal deliveries cannot go. Start with the basics. Use WASD to move and SPACE to jump."] = "Мы доставляем туда, куда нормальные люди не суются. Начнём просто: WASD - движение, SPACE - прыжок.",
            ["This course hangs in open space, but the training recovery system is active. If you fall, it will return you to your latest checkpoint."] = "И да, внизу открытый космос. Сорвёшься - система вернёт тебя к последней точке. Возможно, даже вместе с достоинством.",
            ["Checkpoint confirmed. Your training record has been updated."] = "Точка сохранена! Если космос тебя съест, вернёшься сюда.",
            ["Next is sprinting. Hold SHIFT while moving to build the speed a field courier needs."] = "Ходить научились. Теперь побежали: двигайся и держи SHIFT, чтобы набрать скорость.",
            ["Use a sprint jump to clear the gap ahead. Trust the recovery system if you miss."] = "Разгон, прыжок, перелёт. Не долетишь - система посмеётся первой.",
            ["Good work, recruit. One movement section remains."] = "Неплохо, новичок. Остался последний участок.",
            ["Cross the three platforms ahead and reach the trial exit."] = "Три платформы и один выход. Не засматривайся на космос.",
            ["Complete this course and report to weapons training. A licensed courier must be ready for hostile delivery zones."] = "Доберись до выхода и отправляйся к оружию. Некоторые клиенты стреляют раньше, чем подписывают доставку.",
            ["Welcome to weapons training. Your first courier sidearm is the pistol."] = "Добро пожаловать на стрельбище. Держи пистолет - ответ Корпуса на особо грубых клиентов.",
            ["Shoot all six targets to verify your aim and activate the next platform."] = "Сбей все шесть мишеней - и следующая платформа твоя. Всё просто.",
            ["Static target calibration complete. Now prove you can aim under pressure."] = "По неподвижным попадать легко. Посмотрим, как справишься с движущимися.",
            ["Land on the platform and shoot all three moving targets before they reach the end. If one escapes, the training sequence resets."] = "Приземлись на платформу и сбей три мишени, пока не сбежали. Упустишь одну - начинаем шоу заново.",
            ["Pass this section and the next course will teach you how weapons can control your movement."] = "Пройди участок, и я покажу самое весёлое: как передвигаться с помощью оружия.",
            ["In real delivery zones, your weapon is more than protection. Recoil can become a movement tool."] = "На настоящем маршруте оружие не только защищает. Направь его правильно - и отдача станет двигателем.",
            ["Every weapon applies a different amount of force. Learning that force can keep a courier alive."] = "Каждая пушка лягается по-своему. Запомни отдачу - она спасёт и груз, и твою шкуру.",
            ["This course uses the shotgun. Jump and fire at the ground beneath you to boost upward."] = "Бери дробовик. Прыгай, стреляй под ноги и знакомься с полётами.",
            ["Remember this before you enter a live delivery zone."] = "Запомни этот трюк. На реальном маршруте второй попытки могут не дать.",
            ["The shotgun gives you a stronger push when you fire at a nearby surface than when you fire into open space."] = "Чем ближе поверхность, тем сильнее дробовик тебя швырнёт. Хоть где-то физика помогает.",
            ["Use that extra force to reach the next platform."] = "Лови отдачу и запускай себя на следующую платформу.",
            ["Good. Now combine weapon handling with movement."] = "Хорошо. Теперь совместим стрельбу с движением. Постарайся сохранить все конечности.",
            ["Use the pistol to clear the targets and activate the platform."] = "Сними мишени из пистолета и разбуди платформу.",
            ["Then use the shotgun to reach the finish. Face away from it, move BACKWARD, and fire to push yourself toward the exit."] = "Теперь дробовик. Повернись спиной к выходу, двигайся НАЗАД, стреляй и лети к финишу.",
            ["This trial adds the rifle, the Corps standard for sustained fire. Keep moving while you clear groups of targets."] = "Знакомься с винтовкой: громкая, быстрая и отлично расчищает путь. Только не стой на месте.",
            ["Its recoil gives only a small movement push. Its strength is clearing several threats without breaking your flow."] = "Далеко она тебя не запустит, зато быстро убирает мишени и не сбивает темп.",
            ["Clear the seven targets ahead to open the first gate."] = "Семь мишеней мешают открыть ворота. Исправь это.",
            ["Fast deliveries demand control in tight spaces. Hold LEFT CTRL while moving downhill to begin a slide."] = "Впереди тесно. Двигайся вниз и держи LEFT CTRL, чтобы проскользнуть под проблемами.",
            ["Stay low beneath the gate, then jump from the runway to carry your speed across the gap."] = "Не поднимай голову под воротами, а с разгона прыгай через разрыв.",
            ["Stay low, then jump from the runway to carry your speed across the gap and jump again in timing to keep momentum."] = "Пригнись, вылетай с разгона и вовремя прыгай снова, чтобы не потерять скорость.",
            ["Now combine both lessons. Shoot every moving target while sliding through the final lane."] = "Финальная полоса: скользи быстро, стреляй ещё быстрее и никого не упусти.",
            ["The finish gate opens when the last target falls. Reach it without losing your courier momentum."] = "Последняя мишень откроет финиш. Не теряй скорость и прорывайся.",
            ["Heavy cargo routes may require the bazooka. It carries one rocket, but no other courier weapon can match its impact."] = "Тяжёлый маршрут требует тяжёлого ответа. В базуке одна ракета, но обычно её хватает.",
            ["Fire at the reinforced grid ahead. Only a heavy impact can breach this training barrier."] = "Видишь укреплённую решётку? Познакомь её с ракетой.",
            ["Rockets take time to travel. Start reloading while you continue forward."] = "Ракета летит не мгновенно. Стреляй, перезаряжайся на ходу и не жди фейерверка.",
            ["Now use the blast for movement. Jump, look down and slightly behind you, then fire."] = "Теперь оседлай взрыв. Прыгай, смотри вниз и немного назад, затем стреляй.",
            ["The blast pushes you away from its impact point. Hold forward in the air and land on the platform above."] = "Взрыв толкает от точки удара. В воздухе держи движение вперёд и садись на верхнюю платформу.",
            ["A trained Corps courier can also use a bazooka to redirect in mid-air."] = "Хороший курьер умеет менять направление даже в воздухе. Ракета поможет.",
            ["Jump forward and fire at the platform on the left. Its blast will push you right toward the next platform."] = "Прыгай вперёд, стреляй в платформу слева - взрыв швырнёт тебя вправо.",
            ["Final sequence. Breach the reinforced grid and reload while running."] = "Финальный заход: ломай решётку и перезаряжайся, не сбавляя темп.",
            ["At the edge, jump and fire at the platform behind you. Ride the blast toward the finish."] = "У края прыгай и стреляй назад. Дальше взрыв доставит тебя к финишу.",
            ["Recruit, this is your Space Delivery Corps licensing exam. Cross the start line to begin the timer, then use everything you learned to reach the finish. Pass this test and your courier license will unlock real delivery assignments. Training recovery remains active here; outside this facility, it will not save you."] = "Вот и всё, новичок - экзамен на лицензию. Пересеки старт, пройди трассу и заслужи реальные заказы. Здесь система ещё спасёт тебя. Снаружи вытаскивать будет некому."
        };

        public static event Action LanguageChanged;
        public static IReadOnlyDictionary<string, string> DefaultRussianEntries => RussianDefaults;

        public static GameLanguage CurrentLanguage
        {
            get => (GameLanguage)Mathf.Clamp(PlayerPrefs.GetInt(LanguageKey, 0), 0, 1);
            set
            {
                if (CurrentLanguage == value)
                    return;

                PlayerPrefs.SetInt(LanguageKey, (int)value);
                PlayerPrefs.Save();
                ApplyToLoadedText();
                LanguageChanged?.Invoke();
            }
        }

        public static string Translate(string english)
        {
            if (string.IsNullOrEmpty(english))
                return english;

            if (TryGetDialogueTone(english, out string rewrittenDialogue))
                return rewrittenDialogue;

            EnsureTablesLoaded();
            LocalizationTable table = CurrentLanguage == GameLanguage.Russian
                ? _russianTable
                : _englishTable;
            if (table != null)
                return table.Get(english);

            if (CurrentLanguage == GameLanguage.Russian &&
                RussianDefaults.TryGetValue(english, out string translated))
            {
                return translated;
            }

            return english;
        }

        public static void ReloadTables()
        {
            _englishTable = null;
            _russianTable = null;
            EnsureTablesLoaded();
        }

        private static void EnsureTablesLoaded()
        {
            if (_englishTable == null)
                _englishTable = Resources.Load<LocalizationTable>(EnglishTablePath);
            if (_russianTable == null)
                _russianTable = Resources.Load<LocalizationTable>(RussianTablePath);
        }

        public static void SetText(TMP_Text text, string english)
        {
            if (text == null)
                return;

            english ??= string.Empty;
            TmpSources[text] = english;
            if (!TmpAutoSizeSettings.ContainsKey(text))
                TmpAutoSizeSettings[text] = new TmpAutoSizeDefaults(text);
            if (!TmpFonts.ContainsKey(text))
                TmpFonts[text] = text.font;
            Apply(text, english);
        }

        public static void ApplyToLoadedText()
        {
            TMP_Text[] tmpTexts = UnityEngine.Object.FindObjectsByType<TMP_Text>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < tmpTexts.Length; i++)
            {
                TMP_Text text = tmpTexts[i];
                if (!TmpSources.TryGetValue(text, out string source))
                {
                    source = text.text;
                    TmpSources[text] = source;
                }

                if (!TmpAutoSizeSettings.ContainsKey(text))
                    TmpAutoSizeSettings[text] = new TmpAutoSizeDefaults(text);
                if (!TmpFonts.ContainsKey(text))
                    TmpFonts[text] = text.font;
                Apply(text, source);
            }

            Text[] legacyTexts = UnityEngine.Object.FindObjectsByType<Text>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < legacyTexts.Length; i++)
            {
                Text text = legacyTexts[i];
                if (!LegacySources.TryGetValue(text, out string source))
                {
                    source = text.text;
                    LegacySources[text] = source;
                }

                Apply(text, source);
            }
        }

        private static void Apply(TMP_Text text, string source)
        {
            LocalizationTable table = GetCurrentTable();
            string value = source;
            bool hasLocalizedEntry = TryGetDialogueTone(source, out value) ||
                                     table != null && table.TryGet(source, out value);
            text.text = hasLocalizedEntry ? value : Translate(source);

            if (CurrentLanguage == GameLanguage.Russian && hasLocalizedEntry)
            {
                if (_cyrillicFont != null)
                    text.font = _cyrillicFont;
            }
            else if (TmpFonts.TryGetValue(text, out TMP_FontAsset originalFont) &&
                     originalFont != null)
            {
                text.font = originalFont;
            }

            if (hasLocalizedEntry)
            {
                TmpAutoSizeDefaults settings = TmpAutoSizeSettings[text];
                text.enableAutoSizing = true;
                text.fontSizeMin = settings.Min;
                text.fontSizeMax = settings.Max;
                text.overflowMode = TextOverflowModes.Truncate;
            }
        }

        private static void Apply(Text text, string source)
        {
            LocalizationTable table = GetCurrentTable();
            bool hasLocalizedEntry = TryGetDialogueTone(source, out string value) ||
                                     table != null && table.TryGet(source, out value);
            text.text = hasLocalizedEntry ? value : Translate(source);
        }

        private static bool TryGetDialogueTone(string source, out string value)
        {
            Dictionary<string, string> dialogueTone = CurrentLanguage == GameLanguage.Russian
                ? RussianDialogueTone
                : EnglishDialogueTone;
            return dialogueTone.TryGetValue(source, out value);
        }

        private static LocalizationTable GetCurrentTable()
        {
            EnsureTablesLoaded();
            return CurrentLanguage == GameLanguage.Russian ? _russianTable : _englishTable;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            _cyrillicFont = Resources.Load<TMP_FontAsset>(
                "Fonts & Materials/LiberationSans SDF - Fallback");
            if (_cyrillicFont != null && !TMP_Settings.fallbackFontAssets.Contains(_cyrillicFont))
                TMP_Settings.fallbackFontAssets.Add(_cyrillicFont);

            var host = new GameObject("LocalizationRuntime");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<LocalizationRuntime>();
        }

        private sealed class LocalizationRuntime : MonoBehaviour
        {
            private void OnEnable() => SceneManager.sceneLoaded += HandleSceneLoaded;
            private void OnDisable() => SceneManager.sceneLoaded -= HandleSceneLoaded;

            private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
            {
                TmpSources.Clear();
                LegacySources.Clear();
                TmpAutoSizeSettings.Clear();
                TmpFonts.Clear();
                StartCoroutine(ApplyNextFrame());
            }

            private static IEnumerator ApplyNextFrame()
            {
                yield return null;
                ApplyToLoadedText();
            }
        }
    }
}
