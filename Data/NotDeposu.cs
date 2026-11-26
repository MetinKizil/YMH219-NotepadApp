using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using NotepadApp.Models;

namespace NotepadApp.Data;

/// <summary>Not listesini yönetir ve JSON dosyasına serileştirir.</summary>
public class NotDeposu
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All) // Türkçe harfler okunur kalsın
    };

    private readonly List<Not> _notlar = new();

    public string DosyaYolu { get; }
    public IReadOnlyList<Not> Notlar => _notlar;

    public NotDeposu(string dosyaYolu) => DosyaYolu = dosyaYolu;

    /// <summary>Dosya yoksa liste boş kalır. Dosya bozuksa JsonException fırlatır.</summary>
    public void Yukle()
    {
        _notlar.Clear();
        if (!File.Exists(DosyaYolu)) return;

        string json = File.ReadAllText(DosyaYolu, Encoding.UTF8);
        List<Not?> okunan = JsonSerializer.Deserialize<List<Not?>>(json, Options)
            ?? throw new JsonException("Dosyada not listesi yok.");

        foreach (Not? not in okunan)
        {
            if (not == null) continue;
            not.Baslik ??= "";
            not.Icerik ??= "";
            _notlar.Add(not);
        }
    }

    public void Kaydet()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(DosyaYolu)!);

        // Önce geçici dosyaya yazılır; yazma yarıda kalırsa asıl dosya bozulmaz.
        string gecici = DosyaYolu + ".tmp";
        File.WriteAllText(gecici, JsonSerializer.Serialize(_notlar, Options), Encoding.UTF8);
        File.Move(gecici, DosyaYolu, overwrite: true);
    }

    /// <summary>Bozuk dosyayı silmeden kenara kopyalar ve yedeğin yolunu döner.</summary>
    public string Yedekle()
    {
        string yedek = $"{DosyaYolu}.bozuk-{DateTime.Now:yyyyMMdd-HHmmss}";
        File.Copy(DosyaYolu, yedek);
        return yedek;
    }

    /// <summary>Başlığı veya içeriği aranan metni içeren notlar (büyük/küçük harf duyarsız).</summary>
    public IEnumerable<Not> Ara(string metin)
    {
        string aranan = metin.Trim();
        if (aranan.Length == 0) return _notlar;

        // Türkçe kültür: "İ-i" ve "I-ı" doğru eşleşir
        var karsilastirma = CultureInfo.GetCultureInfo("tr-TR").CompareInfo;
        bool Icerir(string kaynak) => karsilastirma.IndexOf(kaynak, aranan, CompareOptions.IgnoreCase) >= 0;

        return _notlar.Where(n => Icerir(n.Baslik) || Icerir(n.Icerik));
    }

    public Not Ekle(string baslik, string icerik)
    {
        var not = new Not { Baslik = baslik, Icerik = icerik };
        _notlar.Add(not);
        return not;
    }

    public void Guncelle(Not not, string baslik, string icerik)
    {
        not.Baslik = baslik;
        not.Icerik = icerik;
        not.DuzenlemeTarihi = DateTime.Now;
    }

    public void Sil(Not not) => _notlar.Remove(not);

    /// <summary>Tek bir notu düz metin dosyasına aktarır.</summary>
    public static void DisaAktar(Not not, string yol)
    {
        using var yazici = new StreamWriter(yol, append: false, Encoding.UTF8);
        yazici.WriteLine(not.Baslik);
        yazici.WriteLine($"Oluşturma: {not.OlusturmaTarihi:dd.MM.yyyy HH:mm}");
        yazici.WriteLine($"Düzenleme: {not.DuzenlemeTarihi:dd.MM.yyyy HH:mm}");
        yazici.WriteLine();
        yazici.Write(not.Icerik);
    }
}
