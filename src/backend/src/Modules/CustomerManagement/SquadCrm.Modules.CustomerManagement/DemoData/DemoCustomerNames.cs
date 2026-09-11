namespace SquadCrm.Modules.CustomerManagement.DemoData;

/// <summary>
/// Deterministic synthetic customer names. Obviously fictional; no real person's
/// data is used.
/// <para>
/// Pairing is index-driven rather than random so that <c>(first, last)</c> never
/// repeats within a run: <c>customer[i]</c> takes <c>First[i % First.Length]</c>
/// and <c>Last[(i / First.Length) % Last.Length]</c>. With 40 × 30 = 1200
/// combinations that holds for every supported dataset size, which is what keeps
/// <c>ix_customer_duplicate_match</c> satisfied independently of the department
/// and branch a customer lands in.
/// </para>
/// </summary>
public static class DemoCustomerNames
{
    public static readonly string[] First =
    [
        "Ahmed", "Fatimah", "Mohammed", "Noura", "Abdullah", "Layla", "Khalid", "Maryam",
        "Omar", "Sara", "Yousef", "Hind", "Faisal", "Reem", "Saleh", "Aisha",
        "Turki", "Jawaher", "Bandar", "Lama", "Majed", "Ghada", "Nasser", "Amal",
        "Ibrahim", "Rana", "Hassan", "Dana", "Tariq", "Salma", "Waleed", "Huda",
        "Ziad", "Manal", "Rayan", "Bushra", "Sultan", "Wafa", "Adel", "Shatha",
    ];

    public static readonly string[] Last =
    [
        "Al-Harbi", "Al-Qahtani", "Al-Otaibi", "Al-Ghamdi", "Al-Zahrani", "Al-Shehri",
        "Al-Dosari", "Al-Anazi", "Al-Mutairi", "Al-Subaie", "Al-Rashid", "Al-Faisal",
        "Al-Amri", "Al-Juhani", "Al-Balawi", "Al-Maliki", "Al-Shammari", "Al-Yami",
        "Nasr", "Haddad", "Khoury", "Mansour", "Saleem", "Farouk",
        "Rahman", "Siddiqui", "Bakr", "Idris", "Hamdan", "Nazir",
    ];

    /// <summary>The unique name pair for the customer at <paramref name="index"/>.</summary>
    public static (string First, string Last) At(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        return (First[index % First.Length], Last[index / First.Length % Last.Length]);
    }

    /// <summary>How many distinct pairs <see cref="At"/> can produce before it repeats.</summary>
    public static int Capacity => First.Length * Last.Length;
}
