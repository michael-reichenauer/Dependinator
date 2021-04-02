import FileSaver from 'file-saver'

export default class StoreFiles {
    loadFile(resultHandler) {
        this.load(fileText => {
            if (fileText == null) {
                console.warn('Failed to read file')
                resultHandler(null)
                return
            }

            const file = JSON.parse(fileText)
            if (file == null) {
                console.warn('Failed to parse file')
                resultHandler(null)
                return
            }

            resultHandler(file)
        })
    }

    saveFile(fileName, file) {
        const fileText = JSON.stringify(file, null, 2)
        const blob = new Blob([fileText], { type: "text/plain;charset=utf-8" });
        FileSaver.saveAs(blob, fileName);

    }

    load(resultHandler) {
        const readFile = this.buildFileSelector(e => {
            var file = e.path[0].files[0];
            if (!file) {
                console.log('No file')
                resultHandler(null);
            }

            const reader = new FileReader();
            reader.onload = e => resultHandler(e.target.result)
            reader.onerror = e => resultHandler(null)

            reader.readAsText(file);
        })

        // Trigger browser to show 'open file' dialog to read file
        readFile.click()
    }


    buildFileSelector(selectedHandler) {
        const fileSelector = document.createElement('input');
        fileSelector.setAttribute('type', 'file');
        fileSelector.setAttribute('multiple', 'multiple');
        fileSelector.addEventListener('change', selectedHandler, false);

        return fileSelector;
    }
}