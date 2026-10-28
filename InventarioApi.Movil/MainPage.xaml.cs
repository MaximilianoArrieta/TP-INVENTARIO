namespace InventarioApi.Movil
{
    public partial class MainPage : ContentPage
    {
        public MainPage()
        {
            InitializeComponent();

#if ANDROID
            // Con el servidor local (http://10.0.2.2) las fotos de productos llegan por http
            // y la app corre en https: sin esto el WebView las bloquea.
            blazorWebView.BlazorWebViewInitialized += (_, e) =>
                e.WebView.Settings.MixedContentMode = Android.Webkit.MixedContentHandling.AlwaysAllow;
#endif
        }
    }
}
