using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace InventarioApi.Api.Services;

// Envío de correos con el PDF adjunto. Dos formas, según "Correo:Proveedor":
//   "Smtp"  (por defecto): un servidor SMTP, por ejemplo Gmail con contraseña de aplicación.
//   "Brevo": la API web de Brevo (por HTTPS). Sirve cuando el hosting bloquea el SMTP,
//            como pasa en el plan gratuito de MonsterASP.
public class CorreoService
{
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpFactory;

    public CorreoService(IConfiguration config, IHttpClientFactory httpFactory)
    {
        _config = config;
        _httpFactory = httpFactory;
    }

    private bool UsaBrevo => string.Equals(_config["Correo:Proveedor"], "Brevo", StringComparison.OrdinalIgnoreCase);

    private string? Remitente =>
        string.IsNullOrWhiteSpace(_config["Correo:Remitente"]) ? _config["Correo:Usuario"] : _config["Correo:Remitente"];

    private string NombreRemitente => _config["Correo:NombreRemitente"] ?? "Caja y Stock";

    public bool Configurado => UsaBrevo
        ? !string.IsNullOrWhiteSpace(_config["Correo:BrevoApiKey"]) && !string.IsNullOrWhiteSpace(Remitente)
        : !string.IsNullOrWhiteSpace(_config["Correo:Host"]) &&
          !string.IsNullOrWhiteSpace(_config["Correo:Usuario"]) &&
          !string.IsNullOrWhiteSpace(_config["Correo:Password"]);

    public static bool EsEmailValido(string? email) =>
        !string.IsNullOrWhiteSpace(email) && MailboxAddress.TryParse(email.Trim(), out _);

    public Task EnviarAsync(string destinatario, string asunto, string cuerpoHtml, byte[] adjunto, string nombreAdjunto) =>
        UsaBrevo
            ? EnviarPorBrevoAsync(destinatario.Trim(), asunto, cuerpoHtml, adjunto, nombreAdjunto)
            : EnviarPorSmtpAsync(destinatario.Trim(), asunto, cuerpoHtml, adjunto, nombreAdjunto);

    private async Task EnviarPorSmtpAsync(string destinatario, string asunto, string cuerpoHtml, byte[] adjunto, string nombreAdjunto)
    {
        var mensaje = new MimeMessage();
        mensaje.From.Add(new MailboxAddress(NombreRemitente, Remitente));
        mensaje.To.Add(MailboxAddress.Parse(destinatario));
        mensaje.Subject = asunto;

        var cuerpo = new BodyBuilder { HtmlBody = cuerpoHtml };
        cuerpo.Attachments.Add(nombreAdjunto, adjunto, new ContentType("application", "pdf"));
        mensaje.Body = cuerpo.ToMessageBody();

        var puerto = _config.GetValue("Correo:Puerto", 587);
        var seguridad = puerto == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;

        using var smtp = new SmtpClient();
        await smtp.ConnectAsync(_config["Correo:Host"], puerto, seguridad);
        await smtp.AuthenticateAsync(_config["Correo:Usuario"], _config["Correo:Password"]);
        await smtp.SendAsync(mensaje);
        await smtp.DisconnectAsync(true);
    }

    private async Task EnviarPorBrevoAsync(string destinatario, string asunto, string cuerpoHtml, byte[] adjunto, string nombreAdjunto)
    {
        var cliente = _httpFactory.CreateClient();
        using var pedido = new HttpRequestMessage(HttpMethod.Post, "https://api.brevo.com/v3/smtp/email");
        pedido.Headers.Add("api-key", _config["Correo:BrevoApiKey"]);
        pedido.Content = JsonContent.Create(new
        {
            sender = new { name = NombreRemitente, email = Remitente },
            to = new[] { new { email = destinatario } },
            subject = asunto,
            htmlContent = cuerpoHtml,
            attachment = new[] { new { name = nombreAdjunto, content = Convert.ToBase64String(adjunto) } }
        });

        using var respuesta = await cliente.SendAsync(pedido);
        if (!respuesta.IsSuccessStatusCode)
        {
            var detalle = await respuesta.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Brevo respondió {(int)respuesta.StatusCode}: {detalle}");
        }
    }
}
