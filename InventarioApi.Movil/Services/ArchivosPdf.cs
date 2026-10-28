namespace InventarioApi.Movil.Services;

// Manejo de los reportes PDF en el celular: guardarlos, verlos dentro de la app,
// abrirlos con otra app o compartirlos (WhatsApp, Gmail, Drive...).
public class ArchivosPdf
{
    public async Task<string> GuardarAsync(byte[] contenido, string nombreArchivo)
    {
        var ruta = Path.Combine(FileSystem.Current.CacheDirectory, nombreArchivo);
        await File.WriteAllBytesAsync(ruta, contenido);
        return ruta;
    }

    // Cada página como imagen PNG (data URI) para mostrarla en la app.
    // El WebView de Android no muestra PDF, así que se usa el lector de PDF nativo de Android.
    public Task<List<string>> PaginasComoImagenesAsync(string ruta, int anchoPixeles = 1100) =>
        Task.Run(() => LectorPdf.PaginasComoImagenes(ruta, anchoPixeles));

    public Task CompartirAsync(string ruta, string titulo) =>
        Share.Default.RequestAsync(new ShareFileRequest
        {
            Title = titulo,
            File = new ShareFile(ruta, "application/pdf")
        });

    public Task<bool> AbrirConOtraAppAsync(string ruta, string titulo) =>
        Launcher.Default.OpenAsync(new OpenFileRequest(titulo, new ReadOnlyFile(ruta, "application/pdf")));
}
