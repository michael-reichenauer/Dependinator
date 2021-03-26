import FileSaver from 'file-saver'

const diagramKey = 'diagram'
const lastUsedDiagramKey = 'lastUsedDiagram'
const rootCanvasId = 'root'

class Store {
    getLastUsedDiagramId() {
        return this.readData(lastUsedDiagramKey)?.id
    }

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

    readDiagramRootCanvas(diagramId) {
        if (diagramId == null) {
            return null
        }
        const diagramData = this.readData(this.diagramKey(diagramId))
        if (diagramData == null) {
            return null
        }
        this.writeData(lastUsedDiagramKey, { id: diagramId })

        return this.readCanvas(diagramId, rootCanvasId)
    }

    newDiagram(diagramId, systemId, name) {
        const diagramData = { systemId: systemId, name: name }
        this.writeData(this.diagramKey(diagramId), diagramData)
        this.writeData(lastUsedDiagramKey, { id: diagramId })
    }

    setDiagramName(diagramId, name) {
        const diagramData = this.readData(this.diagramKey(diagramId))
        this.writeData(this.diagramKey(diagramId), { ...diagramData, name: name })
    }

    readCanvas(diagramId, canvasId) {
        return this.readData(this.canvasKey(diagramId, canvasId))
    }


    writeCanvas(canvasData, canvasId) {
        this.writeData(this.canvasKey(canvasData.diagramId, canvasId), canvasData)
    }

    buildFileSelector(selectedHandler) {
        const fileSelector = document.createElement('input');
        fileSelector.setAttribute('type', 'file');
        fileSelector.setAttribute('multiple', 'multiple');
        fileSelector.addEventListener('change', selectedHandler, false);

        return fileSelector;
    }

    readData(key) {
        let text = localStorage.getItem(key)
        if (text == null) {
            console.log('No data for key', key)
            return null
        }
        return JSON.parse(text)
    }

    writeData(key, data) {
        const text = JSON.stringify(data)
        localStorage.setItem(key, text)
    }

    canvasKey(diagramId, canvasId) {
        return `${diagramKey}.${diagramId}.${canvasId}`
    }

    diagramKey(diagramId) {
        return `${diagramKey}.${diagramId}.DiagramData`
    }
}

export const store = new Store()

