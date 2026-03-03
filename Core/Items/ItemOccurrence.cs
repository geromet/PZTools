using Data.Data;

namespace Core.Items;

public readonly record struct ItemOccurrence(
    Distribution Distribution,
    Container? Container,   // null = distribution-level
    bool IsJunk,
    int Index               // index in ItemChances/JunkChances at build time
);
