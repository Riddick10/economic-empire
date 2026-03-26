namespace GrandStrategyGame;

/// <summary>
/// Zentrale Konfiguration fuer Spieleinstellungen und Konstanten
/// </summary>
public static class GameConfig
{
    // === FENSTER ===
    public const int WINDOW_WIDTH = 1600;
    public const int WINDOW_HEIGHT = 900;
    public const int WINDOW_MIN_WIDTH = 1024;
    public const int WINDOW_MIN_HEIGHT = 600;

    // === UI LAYOUT (angepasst fuer VCR OSD Mono) ===
    public const int PANEL_WIDTH = 520;
    public const int PANEL_MARGIN = 10;
    public const int TOP_BAR_HEIGHT = 60;
    public const int BOTTOM_BAR_HEIGHT = 35;
    public const int PANEL_PADDING = 18;

    // === SCHRIFTGROESSEN ===
    public const int FONT_SIZE_TITLE = 60;
    public const int FONT_SIZE_HEADER = 32;
    public const int FONT_SIZE_LARGE = 24;
    public const int FONT_SIZE_NORMAL = 18;
    public const int FONT_SIZE_SMALL = 14;

    // === BUTTONS ===
    public const int BUTTON_HEIGHT = 40;
    public const int BUTTON_BORDER = 2;
    public const float BUTTON_ROUNDNESS = 0.1f;

    // === ICONS ===
    public const int ICON_SIZE_SMALL = 16;
    public const int ICON_SIZE_MEDIUM = 24;
    public const int ICON_SIZE_LARGE = 32;
    public const int FLAG_HEIGHT = 50;
    public const int PORTRAIT_HEIGHT = 110;

    // === KARTE ===
    public const float MAX_ZOOM = 50.0f;  // Erhoet fuer naeheres Reinzoomen
    public const float MIN_ZOOM = 0.5f;
    public const float ZOOM_SPEED = 0.1f;
    public const float PAN_SPEED = 8.0f;

    // === TOOLTIPS ===
    public const int TOOLTIP_OFFSET = 15;
    public const int TOOLTIP_PADDING = 12;

    // === LADEBALKEN ===
    public const int LOADING_BAR_WIDTH = 400;
    public const int LOADING_BAR_HEIGHT = 20;

    // === SPIELMECHANIK ===
    public const double FOOD_PER_PERSON_PER_DAY = 0.000001;  // 1 Nahrung pro 1 Mio Einwohner
    public const double INITIAL_FOOD_DAYS = 365;  // Startvorrat: 1 Jahr Nahrung
}

/// <summary>
/// Laender-IDs als Konstanten (vermeidet Magic Strings)
/// </summary>
public static class CountryIds
{
    public const string Germany = "DEU";
    public const string USA = "USA";
    public const string Russia = "RUS";
    public const string China = "CHN";
    public const string France = "FRA";
    public const string UK = "GBR";
    public const string Japan = "JPN";
    public const string India = "IND";
    public const string Brazil = "BRA";
    public const string Canada = "CAN";
    public const string Australia = "AUS";
    public const string SouthKorea = "KOR";
    public const string Italy = "ITA";
    public const string Spain = "ESP";
    public const string Poland = "POL";
    public const string Ukraine = "UKR";
    public const string Turkey = "TUR";
    public const string SaudiArabia = "SAU";
    public const string Iran = "IRN";
    public const string Israel = "ISR";
    public const string Norway = "NOR";
    public const string Sweden = "SWE";
    public const string Belarus = "BLR";
    public const string Finland = "FIN";
    public const string Austria = "AUT";
    public const string CzechRepublic = "CZE";
    public const string Switzerland = "CHE";
    public const string Mexico = "MEX";
    public const string Kazakhstan = "KAZ";
    public const string Greenland = "GRL";
    public const string Argentina = "ARG";
    public const string Peru = "PER";
    public const string Chile = "CHL";
    public const string Egypt = "EGY";
    public const string Hungary = "HUN";
    public const string Romania = "ROU";
    public const string Bulgaria = "BGR";
    public const string Greece = "GRC";
    public const string Portugal = "PRT";
    public const string Netherlands = "NLD";
    public const string Belgium = "BEL";
    public const string Luxembourg = "LUX";
    public const string Ireland = "IRL";
    public const string Denmark = "DNK";
    public const string Iceland = "ISL";
    public const string Serbia = "SRB";
    public const string Croatia = "HRV";
    public const string Slovakia = "SVK";
    public const string Slovenia = "SVN";
    public const string Bosnia = "BIH";
    public const string Montenegro = "MNE";
    public const string Lithuania = "LTU";
    public const string Latvia = "LVA";
    public const string Estonia = "EST";
    public const string Moldova = "MDA";
    public const string Albania = "ALB";
    public const string NorthMacedonia = "MKD";
    public const string Colombia = "COL";
    public const string Venezuela = "VEN";
    public const string Ecuador = "ECU";
    public const string Bolivia = "BOL";
    public const string Paraguay = "PRY";
    public const string Uruguay = "URY";
    public const string Guyana = "GUY";
    // Afrika
    public const string Libya = "LBY";
    public const string Tunisia = "TUN";
    public const string Algeria = "DZA";
    public const string Morocco = "MAR";
    public const string WesternSahara = "ESH";
    public const string Sudan = "SDN";
    public const string SouthSudan = "SSD";
    public const string Nigeria = "NGA";
    public const string Ghana = "GHA";
    public const string IvoryCoast = "CIV";
    public const string Senegal = "SEN";
    public const string Mali = "MLI";
    public const string BurkinaFaso = "BFA";
    public const string Niger = "NER";
    public const string Guinea = "GIN";
    public const string Benin = "BEN";
    public const string Togo = "TGO";
    public const string SierraLeone = "SLE";
    public const string Liberia = "LBR";
    public const string Mauritania = "MRT";
    public const string Gambia = "GMB";
    public const string DRC = "COD";
    public const string Congo = "COG";
    public const string Cameroon = "CMR";
    public const string Chad = "TCD";
    public const string CAR = "CAF";
    public const string Gabon = "GAB";
    public const string EquatorialGuinea = "GNQ";
    public const string Ethiopia = "ETH";
    public const string Kenya = "KEN";
    public const string Tanzania = "TZA";
    public const string Uganda = "UGA";
    public const string Rwanda = "RWA";
    public const string Burundi = "BDI";
    public const string Somalia = "SOM";
    public const string Eritrea = "ERI";
    public const string Djibouti = "DJI";
    public const string Malawi = "MWI";
    public const string Zambia = "ZMB";
    public const string Zimbabwe = "ZWE";
    public const string Mozambique = "MOZ";
    public const string Madagascar = "MDG";
    public const string SouthAfrica = "ZAF";
    public const string Namibia = "NAM";
    public const string Botswana = "BWA";
    public const string Angola = "AGO";
    public const string Eswatini = "SWZ";
    public const string Lesotho = "LSO";
    // Asien
    public const string Iraq = "IRQ";
    public const string Syria = "SYR";
    public const string Jordan = "JOR";
    public const string Lebanon = "LBN";
    public const string Yemen = "YEM";
    public const string Oman = "OMN";
    public const string UAE = "ARE";
    public const string Kuwait = "KWT";
    public const string Qatar = "QAT";
    public const string Bahrain = "BHR";
    public const string Afghanistan = "AFG";
    public const string Pakistan = "PAK";
    public const string Uzbekistan = "UZB";
    public const string Turkmenistan = "TKM";
    public const string Tajikistan = "TJK";
    public const string Kyrgyzstan = "KGZ";
    public const string Mongolia = "MNG";
    public const string Bangladesh = "BGD";
    public const string SriLanka = "LKA";
    public const string Nepal = "NPL";
    public const string Myanmar = "MMR";
    public const string Thailand = "THA";
    public const string Vietnam = "VNM";
    public const string Cambodia = "KHM";
    public const string Laos = "LAO";
    public const string Malaysia = "MYS";
    public const string Indonesia = "IDN";
    public const string Philippines = "PHL";
    public const string Singapore = "SGP";
    public const string Brunei = "BRN";
    public const string EastTimor = "TLS";
    public const string NorthKorea = "PRK";
    public const string Taiwan = "TWN";
    // Mittelamerika / Karibik
    public const string Panama = "PAN";
    public const string CostaRica = "CRI";
    public const string Nicaragua = "NIC";
    public const string Honduras = "HND";
    public const string Guatemala = "GTM";
    public const string Haiti = "HTI";
    public const string DominicanRepublic = "DOM";
    public const string Cuba = "CUB";
}
