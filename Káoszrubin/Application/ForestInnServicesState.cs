namespace KaoszRubin.Application;

/// <summary>Fogadónként mentett készlet és vendégek, abszolút játékperces időbélyegekkel.</summary>
public sealed record ForestInnServicesState
{
    public long FirstVisitMinutes { get; init; }
    public long RestockThroughMinutes { get; init; }
    public long VisitorsThroughMinutes { get; init; }
    public long RecruitsThroughMinutes { get; init; }
    public int SecretStashAccessCost { get; init; }
    public int FeastPrice { get; init; }
    public int RoomPricePerPerson { get; init; }
    public int RecruitCapacity { get; init; }
    public List<ForestInnVendorState> Vendors { get; init; } = [];
    public Dictionary<string, int> BuybackPrices { get; init; } = [];
    public List<ForestInnRecruitState> Recruits { get; init; } = [];
    public Dictionary<Guid, int> SpecialRecruitPrices { get; init; } = [];
    public List<InnRumor> Rumors { get; init; } = [];
    public List<ForestInnOfferState>? SecretStash { get; init; }
    public List<ForestInnOfferState>? SecretStashTarget { get; init; }
    public int SecretStashRestockCursor { get; init; }
}

public sealed record ForestInnOfferState(string ItemId, int Price, int Quantity, int StockCount);
public sealed record ForestInnVendorState(InnVendorKind Kind, List<ForestInnOfferState> Stock,
    List<ForestInnOfferState> TargetStock, int RestockCursor);
public sealed record ForestInnRecruitState(string CharacterJson, int Price);
