using Microsoft.Maui.Graphics.Platform;

namespace InventarioApi.Movil.Services;

// Foto de un producto con la cámara o desde la galería del celular (solo Admin).
// Se achica antes de subirla para que no viaje una foto de varios MB.
public class FotosProducto
{
    public async Task<Stream?> ObtenerAsync(bool usarCamara)
    {
        FileResult? foto;
        if (usarCamara)
        {
            if (!MediaPicker.Default.IsCaptureSupported)
                throw new InvalidOperationException("Este dispositivo no tiene cámara disponible.");

            var permiso = await MainThread.InvokeOnMainThreadAsync(() => Permissions.RequestAsync<Permissions.Camera>());
            if (permiso != PermissionStatus.Granted)
                throw new InvalidOperationException("Para sacar la foto hay que permitir el uso de la cámara.");

            foto = await MediaPicker.Default.CapturePhotoAsync();
        }
        else
        {
            foto = await MediaPicker.Default.PickPhotoAsync();
        }

        if (foto == null) return null; // el usuario canceló

        using var original = await foto.OpenReadAsync();
        var imagen = PlatformImage.FromStream(original);
        var reducida = imagen.Downsize(1024, true);

        var salida = new MemoryStream();
        reducida.Save(salida, ImageFormat.Jpeg, 0.85f);
        salida.Position = 0;
        return salida;
    }
}
