namespace NotepadApp.Models;

/// <summary>Tek bir not. Bu sınıfın özellikleri JSON dosyasına yazılır.</summary>
public class Not
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Baslik { get; set; } = "";
    public string Icerik { get; set; } = "";
    public DateTime OlusturmaTarihi { get; set; } = DateTime.Now;
    public DateTime DuzenlemeTarihi { get; set; } = DateTime.Now;

    // ListBox bu metni gösterir.
    public override string ToString() => $"{Baslik}  ({DuzenlemeTarihi:dd.MM.yyyy HH:mm})";
}
