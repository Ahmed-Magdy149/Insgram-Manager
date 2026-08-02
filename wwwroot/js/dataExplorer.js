window.downloadFileFromStream = function (fileName, dataUri) {
    const anchor = document.createElement('a');
    anchor.href = dataUri;
    anchor.download = fileName;
    document.body.appendChild(anchor);
    anchor.click();
    document.body.removeChild(anchor);
};
