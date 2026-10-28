namespace InventarioApi.Movil.Services;

// Mensajes cortos que aparecen abajo unos segundos ("Agregado al carrito", etc.)
public class AvisosApp
{
    public record Aviso(int Id, string Texto, string? Enlace, string? TextoEnlace, bool EsError);

    private int _contador;
    private CancellationTokenSource? _temporizador;

    public Aviso? Actual { get; private set; }
    public event Action? Cambio;

    public void Mostrar(string texto, string? enlace = null, string? textoEnlace = null, bool esError = false)
    {
        Actual = new Aviso(++_contador, texto, enlace, textoEnlace, esError);
        Cambio?.Invoke();

        _temporizador?.Cancel();
        _temporizador = new CancellationTokenSource();
        var token = _temporizador.Token;
        _ = Task.Delay(TimeSpan.FromSeconds(3.5), token).ContinueWith(t =>
        {
            if (t.IsCanceled) return;
            Actual = null;
            Cambio?.Invoke();
        }, TaskScheduler.Default);
    }

    public void Ocultar()
    {
        Actual = null;
        Cambio?.Invoke();
    }
}
