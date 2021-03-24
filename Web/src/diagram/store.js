import FileSaver from 'file-saver'


class Store {

    saveFile() {
        const blob = new Blob(["Hello, world!"], { type: "text/plain;charset=utf-8" });
        FileSaver.saveAs(blob, "hello world.txt");

    }

    loadFile(fileHandler) {
        const readFile = this.buildFileSelector(e => {
            var file = e.path[0].files[0];
            if (!file) {
                fileHandler(null);
            }

            const reader = new FileReader();
            reader.onload = e => fileHandler(e.target.result)
            reader.onerror = e => fileHandler(null)

            reader.readAsText(file);
        })

        readFile.click()
    }



    onChanged(e) {
        var file = e.path[0].files[0];
        if (!file) {
            return;
        }
        const reader = new FileReader();

        reader.onload = e => {
            var contents = e.target.result;
            console.log('file', contents);
        };
        reader.onerror = e => console.error('error', e)

        reader.readAsText(file);
    }


    clear() {
        localStorage.clear()
    }

    read(storeName) {
        // Get canvas data from local storage.
        let canvasText = localStorage.getItem(storeName)
        if (canvasText == null) {
            return null
        }

        const canvasData = JSON.parse(canvasText)
        if (canvasData == null) {
            console.warn('Failed to parse canvas data', storeName)
            return null
        }
        return canvasData
    }

    write(canvasData, storeName) {
        // Store canvas data in local storage
        const canvasText = JSON.stringify(canvasData)
        localStorage.setItem(storeName, canvasText)
    }

    buildFileSelector(selectedHandler) {
        const fileSelector = document.createElement('input');
        fileSelector.setAttribute('type', 'file');
        fileSelector.setAttribute('multiple', 'multiple');
        fileSelector.addEventListener('change', selectedHandler, false);

        return fileSelector;
    }
}

export const store = new Store()

