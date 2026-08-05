window.downloadFileFromStream = function (fileName, dataUri) {
    const anchor = document.createElement('a');
    anchor.href = dataUri;
    anchor.download = fileName;
    document.body.appendChild(anchor);
    anchor.click();
    document.body.removeChild(anchor);
};

window.dataExplorer = window.dataExplorer || {};

window.dataExplorer.openExternalUrl = function (url) {
    if (!url) {
        return;
    }

    const openedWindow = window.open(url, '_blank', 'noopener,noreferrer');

    if (openedWindow) {
        openedWindow.opener = null;
    }
};
