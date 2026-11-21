using UnityEngine;

/// <summary>
/// The periodic table, as far as the player can climb it: symbols, names (EN / TR), mass
/// numbers and which elements close a shell (the noble gases). Z = electron count; Z 0 is the
/// bare hydrogen ion the run starts as.
/// </summary>
public static class Elements
{
    public const int Count = 118;

    static readonly string[] Symbols =
    {
        "H", "He", "Li", "Be", "B", "C", "N", "O", "F", "Ne", "Na", "Mg", "Al", "Si", "P", "S", "Cl", "Ar",
        "K", "Ca", "Sc", "Ti", "V", "Cr", "Mn", "Fe", "Co", "Ni", "Cu", "Zn", "Ga", "Ge", "As", "Se", "Br", "Kr",
        "Rb", "Sr", "Y", "Zr", "Nb", "Mo", "Tc", "Ru", "Rh", "Pd", "Ag", "Cd", "In", "Sn", "Sb", "Te", "I", "Xe",
        "Cs", "Ba", "La", "Ce", "Pr", "Nd", "Pm", "Sm", "Eu", "Gd", "Tb", "Dy", "Ho", "Er", "Tm", "Yb", "Lu",
        "Hf", "Ta", "W", "Re", "Os", "Ir", "Pt", "Au", "Hg", "Tl", "Pb", "Bi", "Po", "At", "Rn",
        "Fr", "Ra", "Ac", "Th", "Pa", "U", "Np", "Pu", "Am", "Cm", "Bk", "Cf", "Es", "Fm", "Md", "No", "Lr",
        "Rf", "Db", "Sg", "Bh", "Hs", "Mt", "Ds", "Rg", "Cn", "Nh", "Fl", "Mc", "Lv", "Ts", "Og",
    };

    static readonly string[] NamesEn =
    {
        "Hydrogen", "Helium", "Lithium", "Beryllium", "Boron", "Carbon", "Nitrogen", "Oxygen", "Fluorine", "Neon",
        "Sodium", "Magnesium", "Aluminium", "Silicon", "Phosphorus", "Sulfur", "Chlorine", "Argon",
        "Potassium", "Calcium", "Scandium", "Titanium", "Vanadium", "Chromium", "Manganese", "Iron", "Cobalt",
        "Nickel", "Copper", "Zinc", "Gallium", "Germanium", "Arsenic", "Selenium", "Bromine", "Krypton",
        "Rubidium", "Strontium", "Yttrium", "Zirconium", "Niobium", "Molybdenum", "Technetium", "Ruthenium",
        "Rhodium", "Palladium", "Silver", "Cadmium", "Indium", "Tin", "Antimony", "Tellurium", "Iodine", "Xenon",
        "Caesium", "Barium", "Lanthanum", "Cerium", "Praseodymium", "Neodymium", "Promethium", "Samarium",
        "Europium", "Gadolinium", "Terbium", "Dysprosium", "Holmium", "Erbium", "Thulium", "Ytterbium", "Lutetium",
        "Hafnium", "Tantalum", "Tungsten", "Rhenium", "Osmium", "Iridium", "Platinum", "Gold", "Mercury",
        "Thallium", "Lead", "Bismuth", "Polonium", "Astatine", "Radon",
        "Francium", "Radium", "Actinium", "Thorium", "Protactinium", "Uranium", "Neptunium", "Plutonium",
        "Americium", "Curium", "Berkelium", "Californium", "Einsteinium", "Fermium", "Mendelevium", "Nobelium",
        "Lawrencium", "Rutherfordium", "Dubnium", "Seaborgium", "Bohrium", "Hassium", "Meitnerium",
        "Darmstadtium", "Roentgenium", "Copernicium", "Nihonium", "Flerovium", "Moscovium", "Livermorium",
        "Tennessine", "Oganesson",
    };

    static readonly string[] NamesTr =
    {
        "Hidrojen", "Helyum", "Lityum", "Berilyum", "Bor", "Karbon", "Azot", "Oksijen", "Flor", "Neon",
        "Sodyum", "Magnezyum", "Alüminyum", "Silisyum", "Fosfor", "Kükürt", "Klor", "Argon",
        "Potasyum", "Kalsiyum", "Skandiyum", "Titanyum", "Vanadyum", "Krom", "Mangan", "Demir", "Kobalt",
        "Nikel", "Bakır", "Çinko", "Galyum", "Germanyum", "Arsenik", "Selenyum", "Brom", "Kripton",
        "Rubidyum", "Stronsiyum", "İtriyum", "Zirkonyum", "Niyobyum", "Molibden", "Teknesyum", "Rutenyum",
        "Rodyum", "Paladyum", "Gümüş", "Kadmiyum", "İndiyum", "Kalay", "Antimon", "Tellür", "İyot", "Ksenon",
        "Sezyum", "Baryum", "Lantan", "Seryum", "Praseodim", "Neodim", "Prometyum", "Samaryum",
        "Evropiyum", "Gadolinyum", "Terbiyum", "Disprosyum", "Holmiyum", "Erbiyum", "Tulyum", "İterbiyum", "Lutesyum",
        "Hafniyum", "Tantal", "Tungsten", "Renyum", "Osmiyum", "İridyum", "Platin", "Altın", "Cıva",
        "Talyum", "Kurşun", "Bizmut", "Polonyum", "Astatin", "Radon",
        "Fransiyum", "Radyum", "Aktinyum", "Toryum", "Protaktinyum", "Uranyum", "Neptünyum", "Plütonyum",
        "Amerikyum", "Küriyum", "Berkelyum", "Kaliforniyum", "Aynştaynyum", "Fermiyum", "Mendelevyum", "Nobelyum",
        "Lavrensiyum", "Rutherfordiyum", "Dubniyum", "Seaborgiyum", "Bohriyum", "Hassiyum", "Meitneriyum",
        "Darmstadtiyum", "Röntgenyum", "Kopernikyum", "Nihonyum", "Flerovyum", "Moskovyum", "Livermoryum",
        "Tennesin", "Oganesson",
    };

    /// <summary>Symbol for Z (1..118); "?" beyond the known table.</summary>
    public static string Symbol(int z) => z >= 1 && z <= Count ? Symbols[z - 1] : "?";

    public static string Name(int z)
    {
        if (z <= 0) return Loc.T("Hydrogen ion", "Hidrojen iyonu");
        if (z > Count) return Loc.T("Unknown", "Bilinmeyen");
        return Loc.Current == Language.Turkish ? NamesTr[z - 1] : NamesEn[z - 1];
    }

    /// <summary>A full outer shell: He, Ne, Ar, Kr, Xe, Rn, Og (2, 10, 18, 36, 54, 86, 118).</summary>
    public static bool IsNoble(int z)
    {
        int total = 0;
        for (int s = 0; total < z; s++) total += ShellSize(s);
        return total == z && z > 0;
    }

    /// <summary>
    /// Electrons each energy shell holds, in the order they fill: 2, 8, 8, 18, 18, 32, 32, ...
    /// (one orbital = 2 electrons, so 1, 4, 4, 9, 9, 16, 16 orbitals).
    /// </summary>
    public static int ShellSize(int s)
    {
        int k = s <= 0 ? 1 : (s + 1) / 2 + 1;
        return 2 * k * k;
    }

    /// <summary>Roughly how many nucleons (protons + neutrons) the element's nucleus has.</summary>
    public static int MassNumber(int z)
    {
        if (z <= 1) return 1;
        return Mathf.RoundToInt(z * 2f + z * z * 0.0065f);
    }
}
