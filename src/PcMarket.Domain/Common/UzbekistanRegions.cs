namespace PcMarket.Domain.Common;

/// <summary>Reference list of Uzbekistan regions (viloyatlar) plus Tashkent city, used for
/// address entry and delivery-zone selection. Stored on addresses/orders as free text.</summary>
public static class UzbekistanRegions
{
    public static readonly IReadOnlyList<string> All =
    [
        "Toshkent shahri",
        "Toshkent viloyati",
        "Andijon",
        "Buxoro",
        "Farg'ona",
        "Jizzax",
        "Xorazm",
        "Namangan",
        "Navoiy",
        "Qashqadaryo",
        "Qoraqalpog'iston",
        "Samarqand",
        "Sirdaryo",
        "Surxondaryo"
    ];
}
