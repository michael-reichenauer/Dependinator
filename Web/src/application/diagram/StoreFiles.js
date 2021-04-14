import FileSaver from 'file-saver'

export default class StoreFiles {

    async loadFile() {
        const fileText = await this.load()
        return JSON.parse(fileText)
    }

    saveFile(fileName, file) {
        const fileText = JSON.stringify(file, null, 2)
        const blob = new Blob([fileText], { type: "text/plain;charset=utf-8" });
        FileSaver.saveAs(blob, fileName);
    }

    load() {
        return new Promise((resolve, reject) => {
            const readFile = this.buildFileSelector(e => {
                var file = e.path[0].files[0];
                if (!file) {
                    console.log('No file')
                    reject(new Error('No file'));
                }

                const reader = new FileReader();
                reader.onload = e => resolve(e.target.result)
                reader.onerror = e => reject(reader.error)

                reader.readAsText(file);
            })

            // Trigger browser to show 'open file' dialog to read file
            readFile.click()
        })
    }


    buildFileSelector(selectedHandler) {
        const fileSelector = document.createElement('input');
        fileSelector.setAttribute('type', 'file');
        fileSelector.setAttribute('multiple', 'multiple');
        fileSelector.addEventListener('change', selectedHandler, false);

        return fileSelector;
    }
}