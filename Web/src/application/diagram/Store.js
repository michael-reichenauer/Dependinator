import FileSaver from 'file-saver'
import { timers } from 'jquery'

const diagramKey = 'diagram'
const lastUsedDiagramKey = 'lastUsedDiagram'
const rootCanvasId = 'root'

class Store {
    getLastUsedDiagramId() {
        return this.readData(lastUsedDiagramKey)?.id
    }

    getDiagrams() {
        let diagrams = []

        for (var i = 0, len = localStorage.length; i < len; i++) {
            var key = localStorage.key(i);
            if (key.endsWith('.DiagramData')) {
                const value = JSON.parse(localStorage[key])
                const parts = key.split('.')
                const id = parts[1]
                const name = value.name
                diagrams.push({ id: id, name: name, accessed: value.accessed })
            }
        }

        diagrams.sort((i1, i2) => i1.accessed < i2.accessed ? -1 : i1.accessed > i2.accessed ? 1 : 0)
        return diagrams.reverse()
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
        const diagramData = this.readDiagramData(diagramId)
        if (diagramData == null) {
            return null
        }
        //, accessed:Date.now()
        this.writeData(lastUsedDiagramKey, { id: diagramId })

        return this.readCanvas(diagramId, rootCanvasId)
    }

    newDiagram(diagramId, systemId, name) {
        const diagramData = { systemId: systemId, name: name }
        this.writeDiagramData(diagramId, diagramData)
        this.writeData(lastUsedDiagramKey, { id: diagramId })
    }

    deleteDiagram(diagramId) {
        let keys = []

        for (var i = 0, len = localStorage.length; i < len; i++) {
            var key = localStorage.key(i);
            if (key.startsWith(diagramKey)) {
                const parts = key.split('.')
                const id = parts[1]
                if (id === diagramId) {
                    keys.push(key)
                }
            }
        }

        keys.forEach(key => localStorage.removeItem(key))
    }

    setDiagramName(diagramId, name) {
        const diagramData = this.readDiagramData(diagramId)
        this.writeData(this.diagramKey(diagramId), { ...diagramData, name: name })
    }

    readDiagramData(diagramId) {
        let diagramData = this.readData(this.diagramKey(diagramId))
        this.writeDiagramData(diagramId, diagramData)
        return diagramData
    }

    writeDiagramData(diagramId, diagramData) {
        diagramData = { ...diagramData, accessed: Date.now() }
        this.writeData(this.diagramKey(diagramId), diagramData)
    }

    readCanvas(diagramId, canvasId) {
        return this.readData(this.canvasKey(diagramId, canvasId))
    }

    writeCanvas(canvasData, canvasId) {
        this.writeData(this.canvasKey(canvasData.diagramId, canvasId), canvasData)
        // Update access time
        this.writeDiagramData(canvasData.diagramId, this.readDiagramData(canvasData.diagramId))
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

