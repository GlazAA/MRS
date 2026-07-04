// Simple client-side download helper for MAUI BlazorWebView.
window.mrsDownloadTextFile = (fileName, mimeType, text) => {
    try {
        const blob = new Blob([text], { type: mimeType || "text/plain;charset=utf-8" });
        const url = URL.createObjectURL(blob);
        const a = document.createElement("a");
        a.href = url;
        a.download = fileName;
        document.body.appendChild(a);
        a.click();
        a.remove();
        setTimeout(() => URL.revokeObjectURL(url), 1000);
    } catch (e) {
        // Fallback: do nothing (Blazor UI will show errors on our side if needed)
        console.error(e);
    }
};

window.mrsDownloadBase64File = (fileName, mimeType, base64) => {
    try {
        // Преобразуем base64 -> массив байтов -> Blob, чтобы браузер дал скачать файл.
        // Этот путь нужен для бинарников (zip/doc), когда данные пришли из .NET.
        const byteCharacters = atob(base64 || "");
        const byteNumbers = new Array(byteCharacters.length);
        for (let i = 0; i < byteCharacters.length; i++) {
            byteNumbers[i] = byteCharacters.charCodeAt(i);
        }
        const byteArray = new Uint8Array(byteNumbers);
        const blob = new Blob([byteArray], { type: mimeType || "application/octet-stream" });
        const url = URL.createObjectURL(blob);
        const a = document.createElement("a");
        a.href = url;
        a.download = fileName;
        document.body.appendChild(a);
        a.click();
        a.remove();
        setTimeout(() => URL.revokeObjectURL(url), 1000);
    } catch (e) {
        // Ошибка логируется в консоль WebView; текст ошибки также ловится на стороне .NET при invoke.
        console.error(e);
    }
};

