using System.Globalization;

namespace BaoToolsGui.Services;

public static class GenreLocalizationHelper
{
    private static readonly Dictionary<string, string> Vi = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Action"] = "Hành động",
        ["Adventure"] = "Phiêu lưu",
        ["Strategy"] = "Chiến thuật",
        ["Tactics"] = "Chiến thuật",
        ["Tactical"] = "Chiến thuật",
        ["RPG"] = "Nhập vai",
        ["Role-Playing"] = "Nhập vai",
        ["Simulation"] = "Mô phỏng",
        ["Simulator"] = "Mô phỏng",
        ["Sports"] = "Thể thao",
        ["Racing"] = "Đua xe",
        ["Survival"] = "Sinh tồn",
        ["Casual"] = "Phổ thông",
        ["Indie"] = "Indie",
        ["Massively Multiplayer"] = "Nhiều người chơi (MMO)",
        ["Early Access"] = "Tiếp cận sớm",
        ["Free to Play"] = "Miễn phí",
        ["Puzzle"] = "Giải đố",
        ["Shooter"] = "Bắn súng",
        ["Open World"] = "Thế giới mở",
        ["Horror"] = "Kinh dị",
        ["Fighting"] = "Đối kháng",
        ["Platformer"] = "Đi cảnh",
        ["Building"] = "Xây dựng",
        ["Sandbox"] = "Thế giới mở",
        ["Card Game"] = "Thẻ bài",
        ["Board Game"] = "Board Game",
        ["Anime"] = "Anime",
        ["Co-op"] = "Co-op",
        ["Multiplayer"] = "Nhiều người chơi",
        ["Singleplayer"] = "Một người chơi",
        ["Stealth"] = "Lén lút",
        ["Sci-fi"] = "Khoa học viễn tưởng",
        ["Cyberpunk"] = "Cyberpunk",
        ["Post-apocalyptic"] = "Hậu tận thế",
        ["Story Rich"] = "Cốt truyện sâu sắc"
    };

    private static readonly Dictionary<string, string> ZhHans = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Action"] = "动作",
        ["Adventure"] = "冒险",
        ["Strategy"] = "策略",
        ["Tactics"] = "战术",
        ["Tactical"] = "战术",
        ["RPG"] = "角色扮演",
        ["Role-Playing"] = "角色扮演",
        ["Simulation"] = "模拟",
        ["Simulator"] = "模拟",
        ["Sports"] = "体育",
        ["Racing"] = "竞速",
        ["Survival"] = "生存",
        ["Casual"] = "休闲",
        ["Indie"] = "独立",
        ["Massively Multiplayer"] = "大型多人在线",
        ["Early Access"] = "抢先体验",
        ["Free to Play"] = "免费开玩",
        ["Puzzle"] = "解谜",
        ["Shooter"] = "射击",
        ["Open World"] = "开放世界",
        ["Horror"] = "恐怖",
        ["Fighting"] = "格斗",
        ["Platformer"] = "平台解谜",
        ["Building"] = "建造",
        ["Sandbox"] = "沙盒"
    };

    private static readonly Dictionary<string, string> ZhHant = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Action"] = "動作",
        ["Adventure"] = "冒險",
        ["Strategy"] = "策略",
        ["Tactics"] = "戰術",
        ["Tactical"] = "戰術",
        ["RPG"] = "角色扮演",
        ["Role-Playing"] = "角色扮演",
        ["Simulation"] = "模擬",
        ["Simulator"] = "模擬",
        ["Sports"] = "體育",
        ["Racing"] = "競速",
        ["Survival"] = "生存",
        ["Casual"] = "休閒",
        ["Indie"] = "獨立",
        ["Massively Multiplayer"] = "大型多人線上",
        ["Early Access"] = "搶先體驗",
        ["Free to Play"] = "免費遊玩",
        ["Puzzle"] = "解謎",
        ["Shooter"] = "射擊",
        ["Open World"] = "開放世界",
        ["Horror"] = "恐怖",
        ["Fighting"] = "格鬥",
        ["Platformer"] = "平台遊戲",
        ["Building"] = "建造",
        ["Sandbox"] = "沙盒"
    };

    private static readonly Dictionary<string, string> Ja = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Action"] = "アクション",
        ["Adventure"] = "アドベンチャー",
        ["Strategy"] = "ストラテジー",
        ["Tactics"] = "タクティクス",
        ["RPG"] = "RPG",
        ["Role-Playing"] = "RPG",
        ["Simulation"] = "シミュレーション",
        ["Simulator"] = "シミュレーション",
        ["Sports"] = "スポーツ",
        ["Racing"] = "レース",
        ["Survival"] = "サバイバル",
        ["Casual"] = "カジュアル",
        ["Indie"] = "インディー",
        ["Massively Multiplayer"] = "MMO",
        ["Early Access"] = "早期アクセス",
        ["Free to Play"] = "無料プレイ",
        ["Puzzle"] = "パズル",
        ["Shooter"] = "シューティング",
        ["Open World"] = "オープンワールド",
        ["Horror"] = "ホラー"
    };

    private static readonly Dictionary<string, string> Ko = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Action"] = "액션",
        ["Adventure"] = "어드벤처",
        ["Strategy"] = "전략",
        ["Tactics"] = "전술",
        ["RPG"] = "롤플레잉",
        ["Role-Playing"] = "롤플레잉",
        ["Simulation"] = "시뮬레이션",
        ["Simulator"] = "시뮬레이션",
        ["Sports"] = "스포츠",
        ["Racing"] = "레이싱",
        ["Survival"] = "생존",
        ["Casual"] = "캐주얼",
        ["Indie"] = "인디",
        ["Massively Multiplayer"] = "대규모 멀티플레이어",
        ["Early Access"] = "앞서 해보기",
        ["Free to Play"] = "무료",
        ["Puzzle"] = "퍼즐",
        ["Shooter"] = "슈팅",
        ["Open World"] = "오픈 월드",
        ["Horror"] = "공포"
    };

    private static readonly Dictionary<string, string> Ru = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Action"] = "Экшены",
        ["Adventure"] = "Приключения",
        ["Strategy"] = "Стратегии",
        ["Tactics"] = "Тактика",
        ["RPG"] = "Ролевые игры",
        ["Role-Playing"] = "Ролевые игры",
        ["Simulation"] = "Симуляторы",
        ["Simulator"] = "Симуляторы",
        ["Sports"] = "Спортивные игры",
        ["Racing"] = "Гонки",
        ["Survival"] = "Выживание",
        ["Casual"] = "Казуальные игры",
        ["Indie"] = "Инди",
        ["Massively Multiplayer"] = "ММО",
        ["Early Access"] = "Ранний доступ",
        ["Free to Play"] = "Бесплатно",
        ["Puzzle"] = "Головоломки",
        ["Shooter"] = "Шутеры",
        ["Open World"] = "Открытый мир",
        ["Horror"] = "Хоррор"
    };

    private static readonly Dictionary<string, string> Es = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Action"] = "Acción",
        ["Adventure"] = "Aventura",
        ["Strategy"] = "Estrategia",
        ["Tactics"] = "Táctica",
        ["RPG"] = "Rol",
        ["Role-Playing"] = "Rol",
        ["Simulation"] = "Simulación",
        ["Simulator"] = "Simulador",
        ["Sports"] = "Deportes",
        ["Racing"] = "Carreras",
        ["Survival"] = "Supervivencia",
        ["Casual"] = "Casual",
        ["Indie"] = "Indie",
        ["Massively Multiplayer"] = "Multijugador masivo",
        ["Early Access"] = "Acceso anticipado",
        ["Free to Play"] = "Free to Play",
        ["Puzzle"] = "Puzles",
        ["Shooter"] = "Disparos",
        ["Open World"] = "Mundo abierto",
        ["Horror"] = "Terror"
    };

    private static readonly Dictionary<string, string> Fr = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Action"] = "Action",
        ["Adventure"] = "Aventure",
        ["Strategy"] = "Stratégie",
        ["Tactics"] = "Tactique",
        ["RPG"] = "RPG",
        ["Role-Playing"] = "Jeu de rôle",
        ["Simulation"] = "Simulation",
        ["Simulator"] = "Simulateur",
        ["Sports"] = "Sport",
        ["Racing"] = "Course",
        ["Survival"] = "Survie",
        ["Casual"] = "Occasionnel",
        ["Indie"] = "Indépendant",
        ["Massively Multiplayer"] = "MMO",
        ["Early Access"] = "Accès anticipé",
        ["Free to Play"] = "Free-to-play",
        ["Puzzle"] = "Casse-tête",
        ["Shooter"] = "Tir",
        ["Open World"] = "Monde ouvert",
        ["Horror"] = "Horreur"
    };

    private static readonly Dictionary<string, string> De = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Action"] = "Action",
        ["Adventure"] = "Abenteuer",
        ["Strategy"] = "Strategie",
        ["Tactics"] = "Taktik",
        ["RPG"] = "Rollenspiel",
        ["Role-Playing"] = "Rollenspiel",
        ["Simulation"] = "Simulation",
        ["Simulator"] = "Simulator",
        ["Sports"] = "Sport",
        ["Racing"] = "Rennen",
        ["Survival"] = "Überleben",
        ["Casual"] = "Gelegenheitsspiele",
        ["Indie"] = "Indie",
        ["Massively Multiplayer"] = "MMO",
        ["Early Access"] = "Early Access",
        ["Free to Play"] = "Kostenlos",
        ["Puzzle"] = "Rätsel",
        ["Shooter"] = "Shooter",
        ["Open World"] = "Offene Welt",
        ["Horror"] = "Horror"
    };

    private static readonly Dictionary<string, string> Pt = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Action"] = "Ação",
        ["Adventure"] = "Aventura",
        ["Strategy"] = "Estratégia",
        ["Tactics"] = "Tática",
        ["RPG"] = "RPG",
        ["Role-Playing"] = "RPG",
        ["Simulation"] = "Simulação",
        ["Simulator"] = "Simulador",
        ["Sports"] = "Esportes",
        ["Racing"] = "Corrida",
        ["Survival"] = "Sobrevivência",
        ["Casual"] = "Casual",
        ["Indie"] = "Indie",
        ["Massively Multiplayer"] = "MMO",
        ["Early Access"] = "Acesso Antecipado",
        ["Free to Play"] = "Gratuito para Jogar",
        ["Puzzle"] = "Quebra-cabeça",
        ["Shooter"] = "Tiro",
        ["Open World"] = "Mundo Aberto",
        ["Horror"] = "Terror"
    };

    public static string GetLocalized(string? genre)
    {
        if (string.IsNullOrWhiteSpace(genre)) return "Steam";
        string trimmed = genre.Trim();

        var culture = CultureInfo.CurrentUICulture;
        string twoLetter = culture.TwoLetterISOLanguageName.ToLowerInvariant();
        string name = culture.Name;

        if (twoLetter == "vi" && Vi.TryGetValue(trimmed, out var vi)) return vi;
        if (twoLetter == "zh")
        {
            bool isHant = name.Contains("Hant", StringComparison.OrdinalIgnoreCase) ||
                          name.Contains("TW", StringComparison.OrdinalIgnoreCase) ||
                          name.Contains("HK", StringComparison.OrdinalIgnoreCase);
            if (isHant && ZhHant.TryGetValue(trimmed, out var zhT)) return zhT;
            if (ZhHans.TryGetValue(trimmed, out var zhS)) return zhS;
        }
        if (twoLetter == "ja" && Ja.TryGetValue(trimmed, out var ja)) return ja;
        if (twoLetter == "ko" && Ko.TryGetValue(trimmed, out var ko)) return ko;
        if (twoLetter == "ru" && Ru.TryGetValue(trimmed, out var ru)) return ru;
        if (twoLetter == "es" && Es.TryGetValue(trimmed, out var es)) return es;
        if (twoLetter == "fr" && Fr.TryGetValue(trimmed, out var fr)) return fr;
        if (twoLetter == "de" && De.TryGetValue(trimmed, out var de)) return de;
        if (twoLetter == "pt" && Pt.TryGetValue(trimmed, out var pt)) return pt;

        return genre;
    }
}
