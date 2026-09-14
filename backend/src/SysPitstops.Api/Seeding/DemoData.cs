namespace SysPitstops.Api.Seeding;

// The names, plates and services the demo is built from. Kept apart from the
// seeder so the code that assembles the scenario stays readable, and so that
// changing what the bank sees is editing a list, not editing logic.
internal static class DemoData
{
    public static readonly string[] Mechanics =
    [
        "Roberto Silva", "Marcos Oliveira", "Jefferson Santos"
    ];

    public const string Attendant = "Ana Carolina Lima";

    public static readonly (string Name, string Phone)[] Customers =
    [
        ("Carlos Roberto Mendes", "81998801122"),
        ("Fernanda Silva Souza", "81997712233"),
        ("Guilherme de Alencar", "81996623344"),
        ("Cláudia Maria Pires", "81995534455"),
        ("Juliana Mendes Rocha", "81994445566"),
        ("Bruno Reis Tavares", "81993356677"),
        ("Aline Souza Barbosa", "81992267788"),
        ("Maurício Albuquerque", "81991178899"),
        ("Gerusa Kelly Nunes", "81990089900"),
        ("Rafael Torres Lima", "81989990011"),
        ("Patrícia Gomes Farias", "81988801122"),
        ("Eduardo Vasconcelos", "81987712233"),
        ("Simone Andrade Melo", "81986623344"),
        ("Thiago Ferreira Dias", "81985534455"),
        ("Larissa Campos Brito", "81984445566")
    ];

    public static readonly (string Plate, string Brand, string Model, int Year)[] Vehicles =
    [
        ("GHJ4K56", "Fiat", "Strada", 2021),
        ("KPG4A22", "Hyundai", "HB20", 2020),
        ("NJK8A12", "Honda", "Civic LX", 2021),
        ("BRA2E19", "Toyota", "Corolla", 2022),
        ("HTP9B44", "Volkswagen", "Gol", 2018),
        ("QRS7C31", "Jeep", "Renegade", 2019),
        ("MNO5D77", "Chevrolet", "Onix", 2020),
        ("TUV3E88", "Renault", "Sandero", 2017),
        ("XYZ1F09", "Ford", "Ka", 2019),
        ("ABC8G23", "Nissan", "Kicks", 2021),
        ("DEF6H45", "Volkswagen", "Polo", 2022),
        ("GHI4I67", "Fiat", "Argo", 2020),
        ("JKL2J89", "Citroën", "C3", 2018),
        ("MNP0K12", "Peugeot", "208", 2019),
        ("QRT8L34", "Honda", "Fit", 2016),
        ("UVW6M56", "Toyota", "Etios", 2018),
        ("XZA4N78", "Chevrolet", "Prisma", 2017),
        ("BCD2O90", "Hyundai", "Creta", 2022)
    ];

    /// <summary>Sale price and cost, in that order. The margin is not uniform on
    /// purpose: the revenue KPI would look invented if it were.</summary>
    public static readonly (string Sku, string Name, decimal Sale, decimal Cost)[] Parts =
    [
        ("FLT-OL-001", "Filtro de óleo", 48.90m, 22.00m),
        ("FLT-AR-002", "Filtro de ar", 62.50m, 31.00m),
        ("FLT-CB-003", "Filtro de combustível", 74.00m, 38.50m),
        ("OLE-5W30-04", "Óleo 5W30 sintético (litro)", 54.90m, 29.90m),
        ("PAS-FR-005", "Pastilha de freio dianteira (par)", 189.00m, 96.00m),
        ("PAS-TR-006", "Pastilha de freio traseira (par)", 164.00m, 82.00m),
        ("DSC-FR-007", "Disco de freio (par)", 398.00m, 215.00m),
        ("FLU-DOT4-08", "Fluido de freio DOT4", 39.90m, 18.00m),
        ("COR-DEN-009", "Correia dentada", 156.00m, 78.00m),
        ("VEL-IGN-010", "Vela de ignição (jogo)", 128.00m, 61.00m),
        ("BAT-60A-011", "Bateria 60Ah", 489.00m, 312.00m),
        ("AMO-DI-012", "Amortecedor dianteiro (par)", 620.00m, 358.00m),
        ("AMO-TR-013", "Amortecedor traseiro (par)", 540.00m, 305.00m),
        ("PNE-185-014", "Pneu 185/65 R15", 385.00m, 246.00m),
        ("LAM-H4-015", "Lâmpada H4", 34.90m, 14.50m),
        ("PAL-LIM-016", "Palheta limpador (par)", 79.90m, 36.00m),
        ("RAD-AGU-017", "Aditivo de radiador", 42.00m, 19.50m),
        ("EMB-KIT-018", "Kit de embreagem", 890.00m, 545.00m),
        ("ROL-RD-019", "Rolamento de roda", 210.00m, 112.00m),
        ("SEN-OXI-020", "Sensor de oxigênio", 340.00m, 196.00m)
    ];

    /// <summary>Labour, with the price the workshop charges for it. These become
    /// SERVICE items, which never touch stock.</summary>
    public static readonly (string Description, decimal Price)[] Services =
    [
        ("Troca de óleo e filtro", 120.00m),
        ("Alinhamento e balanceamento", 180.00m),
        ("Revisão geral preventiva", 350.00m),
        ("Troca de pastilhas de freio", 160.00m),
        ("Troca de correia dentada", 480.00m),
        ("Diagnóstico eletrônico", 150.00m),
        ("Troca de amortecedores", 420.00m),
        ("Higienização do ar-condicionado", 190.00m),
        ("Troca de embreagem", 750.00m),
        ("Substituição de bateria", 80.00m)
    ];

    public static readonly string[] Complaints =
    [
        "Barulho ao frear, principalmente em velocidade baixa.",
        "Luz de injeção acesa no painel desde a semana passada.",
        "Carro puxando para a direita na estrada.",
        "Revisão dos 40 mil quilômetros.",
        "Ar-condicionado gelando pouco.",
        "Batida seca ao passar em lombada.",
        "Dificuldade para dar partida pela manhã.",
        "Consumo de combustível aumentou bastante.",
        "Vazamento de óleo na garagem.",
        "Embreagem alta e patinando em subida."
    ];

    public static readonly string[] Diagnoses =
    [
        "Pastilhas dianteiras no limite e disco com sulco. Substituição recomendada.",
        "Sensor de oxigênio com leitura fora da faixa. Trocado e apagado o código.",
        "Alinhamento fora de especificação. Corrigido e balanceado.",
        "Revisão executada conforme plano. Nenhuma avaria adicional encontrada.",
        "Amortecedores dianteiros com perda de óleo. Par substituído.",
        "Bateria sem carga e sem retenção. Substituída.",
        "Correia dentada com trincas. Trocada junto com o tensor."
    ];
}
