// Ayudantes para mostrar y descargar los PDF que genera la API.
// Los bytes llegan desde .NET como Uint8Array.
window.archivos = {
    crearUrl: function (bytes, tipo) {
        return URL.createObjectURL(new Blob([bytes], { type: tipo }));
    },
    liberarUrl: function (url) {
        if (url) URL.revokeObjectURL(url);
    },
    descargar: function (url, nombre) {
        const enlace = document.createElement("a");
        enlace.href = url;
        enlace.download = nombre;
        document.body.appendChild(enlace);
        enlace.click();
        enlace.remove();
    }
};
