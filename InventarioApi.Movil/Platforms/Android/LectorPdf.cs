namespace InventarioApi.Movil;

// Convierte cada página de un PDF en una imagen usando PdfRenderer, el lector de PDF que trae Android.
public static class LectorPdf
{
    public static List<string> PaginasComoImagenes(string ruta, int anchoPixeles)
    {
        var paginas = new List<string>();

        var descriptor = Android.OS.ParcelFileDescriptor.Open(new Java.IO.File(ruta), Android.OS.ParcelFileMode.ReadOnly)!;
        var lector = new Android.Graphics.Pdf.PdfRenderer(descriptor);
        try
        {
            for (var i = 0; i < lector.PageCount; i++)
            {
                var pagina = lector.OpenPage(i)!;
                try
                {
                    // El tamaño de la página viene en puntos: se escala al ancho pedido
                    var escala = (float)anchoPixeles / pagina.Width;
                    var alto = (int)(pagina.Height * escala);

                    using var imagen = Android.Graphics.Bitmap.CreateBitmap(anchoPixeles, alto, Android.Graphics.Bitmap.Config.Argb8888!)!;
                    imagen.EraseColor(Android.Graphics.Color.White.ToArgb()); // fondo blanco (el PDF es transparente)
                    pagina.Render(imagen, null, null, Android.Graphics.Pdf.PdfRenderMode.ForDisplay);

                    using var memoria = new MemoryStream();
                    imagen.Compress(Android.Graphics.Bitmap.CompressFormat.Png!, 100, memoria);
                    paginas.Add("data:image/png;base64," + Convert.ToBase64String(memoria.ToArray()));
                }
                finally
                {
                    pagina.Close(); // Android exige cerrar una página antes de abrir la siguiente
                }
            }
        }
        finally
        {
            lector.Close();
            descriptor.Close();
        }

        return paginas;
    }
}
