using System.Text.Json;
using NotepadApp.Data;
using NotepadApp.Models;

namespace NotepadApp;

/// <summary>Arayüz: solda not listesi, sağda başlık ve içerik.</summary>
public class NotepadForm : Form
{
    private readonly NotDeposu _depo = new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "YMH219-NotepadApp", "notlar.json"));

    private readonly ListBox _liste = new() { Dock = DockStyle.Fill, IntegralHeight = false, HorizontalScrollbar = true };
    private readonly TextBox _arama = new() { Dock = DockStyle.Top, PlaceholderText = "Başlık veya içerikte ara" };
    private readonly Panel _solPanel = new() { Dock = DockStyle.Left, Width = 280, Padding = new Padding(6) };
    private readonly TextBox _baslik = new() { Dock = DockStyle.Top, PlaceholderText = "Başlık", MaxLength = 100 };
    private readonly TextBox _icerik = new()
    {
        Dock = DockStyle.Fill, Multiline = true, AcceptsReturn = true, AcceptsTab = true,
        ScrollBars = ScrollBars.Vertical, MaxLength = 0
    };
    private readonly Button _yeniDugme = new() { Text = "Yeni Not", AutoSize = true };
    private readonly Button _kaydetDugme = new() { Text = "Kaydet", AutoSize = true };
    private readonly Button _silDugme = new() { Text = "Sil", AutoSize = true };
    private readonly Button _aktarDugme = new() { Text = "Dışa Aktar (.txt)", AutoSize = true };
    private readonly FlowLayoutPanel _dugmeler = new() { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(6) };
    private readonly MenuStrip _menu = new();
    private readonly ToolStripMenuItem _dosyaMenu = new("Dosya");
    private readonly ToolStripStatusLabel _durum = new();

    private Not? _secili;        // düzenlenen not; yeni notta null
    private bool _degisti;       // kaydedilmemiş değişiklik var mı
    private bool _dolduruluyor;  // kod alanları doldururken olayları yok say
    private bool _kayitEngelli;  // dosya okunamadıysa üzerine yazılmaz

    public NotepadForm()
    {
        Text = "Not Defteri";
        ClientSize = new Size(820, 520);
        MinimumSize = new Size(640, 400);
        StartPosition = FormStartPosition.CenterScreen;

        _dugmeler.Controls.AddRange(new Control[] { _yeniDugme, _kaydetDugme, _silDugme, _aktarDugme });
        _solPanel.Controls.Add(_liste);
        _solPanel.Controls.Add(_arama);
        _arama.TextChanged += (_, _) => ListeyiYenile(_secili);

        var editor = new Panel { Dock = DockStyle.Fill, Padding = new Padding(6) };
        editor.Controls.Add(_icerik);
        editor.Controls.Add(_baslik);

        _dosyaMenu.DropDownItems.Add(MenuItem("Yeni Not", Keys.Control | Keys.N, YeniNot));
        _dosyaMenu.DropDownItems.Add(MenuItem("Kaydet", Keys.Control | Keys.S, () => NotuKaydet()));
        _dosyaMenu.DropDownItems.Add(MenuItem("Notu Sil", Keys.None, NotuSil));
        _dosyaMenu.DropDownItems.Add(MenuItem("Dışa Aktar (.txt)", Keys.Control | Keys.E, DisaAktar));
        _dosyaMenu.DropDownItems.Add(MenuItem("Dosyadan Yeniden Yükle", Keys.F5, YenidenYukle));
        _menu.Items.Add(_dosyaMenu);

        var durumCubugu = new StatusStrip();
        durumCubugu.Items.Add(_durum);

        // Dock sırası: son eklenen önce yerleşir
        Controls.Add(editor);
        Controls.Add(_solPanel);
        Controls.Add(_dugmeler);
        Controls.Add(_menu);
        Controls.Add(durumCubugu);
        MainMenuStrip = _menu;

        _yeniDugme.Click += (_, _) => YeniNot();
        _kaydetDugme.Click += (_, _) => NotuKaydet();
        _silDugme.Click += (_, _) => NotuSil();
        _aktarDugme.Click += (_, _) => DisaAktar();
        _liste.SelectedIndexChanged += (_, _) => SecimDegisti();
        _baslik.TextChanged += (_, _) => AlanDegisti();
        _icerik.TextChanged += (_, _) => AlanDegisti();
        Load += (_, _) => Yukle();
        FormClosing += (_, e) => e.Cancel = !DegisiklikleriSor();
    }

    private static ToolStripMenuItem MenuItem(string metin, Keys kisayol, Action islem) =>
        new(metin, null, (_, _) => islem()) { ShortcutKeys = kisayol };

    private void Yukle()
    {
        _kayitEngelli = false;
        try
        {
            _depo.Yukle();
            _durum.Text = $"{_depo.Notlar.Count} not yüklendi.";
        }
        catch (JsonException)
        {
            BozukDosyayiYedekle();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _kayitEngelli = true;
            HataGoster("Not dosyası okunamadı, kaydetme kapatıldı: " + ex.Message);
        }

        ListeyiYenile(null);
        Goster(null);
    }

    private void BozukDosyayiYedekle()
    {
        try
        {
            string yedek = _depo.Yedekle();
            HataGoster("Not dosyası bozuk. Boş listeyle devam ediliyor.\nEski dosyanın yedeği: " + yedek);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _kayitEngelli = true;
            HataGoster("Not dosyası bozuk ve yedeklenemedi, kaydetme kapatıldı: " + ex.Message);
        }
    }

    private void YenidenYukle()
    {
        if (!DegisiklikleriSor()) return;
        Yukle();
    }

    private void ListeyiYenile(Not? secilecek)
    {
        _dolduruluyor = true;
        _liste.BeginUpdate();
        _liste.Items.Clear();
        foreach (Not not in GorunenNotlar())
            _liste.Items.Add(not);
        _liste.SelectedItem = secilecek; // arama sonucunda yoksa seçim boş kalır
        _liste.EndUpdate();
        _dolduruluyor = false;
    }

    // En son düzenlenen not en üstte durur.
    private IEnumerable<Not> GorunenNotlar() =>
        _depo.Ara(_arama.Text).OrderByDescending(n => n.DuzenlemeTarihi);

    private void Goster(Not? not)
    {
        _dolduruluyor = true;
        _secili = not;
        _baslik.Text = not?.Baslik ?? "";
        _icerik.Text = not?.Icerik ?? "";
        _dolduruluyor = false;
        _degisti = false;
        _silDugme.Enabled = _aktarDugme.Enabled = not != null;

        if (not != null)
            _durum.Text = $"Oluşturma: {not.OlusturmaTarihi:dd.MM.yyyy HH:mm}   Düzenleme: {not.DuzenlemeTarihi:dd.MM.yyyy HH:mm}";
    }

    private void AlanDegisti()
    {
        if (_dolduruluyor) return;
        _degisti = true;
        _durum.Text = "Kaydedilmemiş değişiklik var.";
    }

    private void SecimDegisti()
    {
        if (_dolduruluyor || _liste.SelectedItem is not Not not || not == _secili) return;

        if (DegisiklikleriSor())
        {
            ListeyiYenile(not);
            Goster(not);
        }
        else
        {
            ListeyiYenile(_secili); // eski seçime dön
        }
    }

    private void YeniNot()
    {
        if (!DegisiklikleriSor()) return;

        ListeyiYenile(null);
        Goster(null);
        _durum.Text = "Yeni not.";
        _baslik.Focus();
    }

    /// <summary>Kayıt başarılıysa true döner.</summary>
    private bool NotuKaydet()
    {
        string baslik = _baslik.Text.Trim();
        if (baslik.Length == 0)
        {
            MessageBox.Show(this, "Not için bir başlık yazın.", Text);
            return false;
        }

        if (_secili == null)
            _secili = _depo.Ekle(baslik, _icerik.Text);
        else
            _depo.Guncelle(_secili, baslik, _icerik.Text);

        if (!DosyayaYaz()) return false;

        ListeyiYenile(_secili);
        Goster(_secili);
        return true;
    }

    private void NotuSil()
    {
        if (_secili == null) return;

        DialogResult cevap = MessageBox.Show(this, $"\"{_secili.Baslik}\" silinsin mi?", Text,
            MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
        if (cevap != DialogResult.Yes) return;

        _depo.Sil(_secili);
        bool yazildi = DosyayaYaz();
        ListeyiYenile(null);
        Goster(null);
        if (yazildi) _durum.Text = "Not silindi.";
    }

    private void DisaAktar()
    {
        if (!DegisiklikleriSor() || _secili == null) return;

        // başlıktaki dosya adında kullanılamayan karakterler değiştirilir
        string ad = string.Join("_", _secili.Baslik.Split(Path.GetInvalidFileNameChars()));
        using var pencere = new SaveFileDialog { Filter = "Metin dosyası (*.txt)|*.txt", FileName = ad + ".txt" };
        if (pencere.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            NotDeposu.DisaAktar(_secili, pencere.FileName);
            _durum.Text = "Dışa aktarıldı: " + pencere.FileName;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            HataGoster("Dışa aktarılamadı: " + ex.Message);
        }
    }

    private bool DosyayaYaz()
    {
        if (_kayitEngelli)
        {
            HataGoster("Not dosyası okunamadığı için kaydetme kapalı. Dosya > Yeniden Yükle'yi deneyin.");
            return false;
        }

        try
        {
            _depo.Kaydet();
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            HataGoster("Notlar kaydedilemedi: " + ex.Message);
            return false;
        }
    }

    /// <summary>Devam edilebiliyorsa true; kullanıcı vazgeçerse false döner.</summary>
    private bool DegisiklikleriSor()
    {
        if (!_degisti) return true;

        DialogResult cevap = MessageBox.Show(this, "Değişiklikler kaydedilsin mi?", Text,
            MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);

        if (cevap == DialogResult.Yes) return NotuKaydet();
        if (cevap == DialogResult.No)
        {
            _degisti = false;
            return true;
        }

        return false;
    }

    private void HataGoster(string mesaj) =>
        MessageBox.Show(this, mesaj, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
}
