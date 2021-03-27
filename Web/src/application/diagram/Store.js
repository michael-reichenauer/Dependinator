import FileSaver from 'file-saver'

const diagramKey = 'diagram'
const diagramDataKey = 'DiagramData'
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
            if (key.endsWith(diagramDataKey)) {
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

    loadDiagramFromFile(resultHandler) {
        this.loadFile(fileText => {
            if (fileText == null) {
                resultHandler(null)
                return
            }

            const diagrams = JSON.parse(fileText)
            if (diagrams == null) {
                console.warn('Failed to parse file')
                resultHandler(null)
                return
            }

            let firstDiagramId = null
            diagrams.diagrams.forEach(diagram => {
                if (firstDiagramId == null) {
                    firstDiagramId = diagram.DiagramData.diagramId
                }
                this.writeDiagramObject(diagram)
            })
            resultHandler(firstDiagramId)
        })
    }

    archiveToFile() {
        const diagrams = this.getDiagrams()
            .map(d => this.readDiagramObject(d.id))
            .filter(d => d != null)
        const diagramsObject = { diagrams: diagrams }

        const fileName = `diagrams.json`
        const fileText = JSON.stringify(diagramsObject, null, 2)
        this.saveFile(fileName, fileText)
    }


    saveDiagramToFile(diagramId) {
        const diagramData = this.readDiagramData(diagramId)
        if (diagramData == null) {
            return
        }

        const diagram = this.readDiagramObject(diagramId)
        const diagrams = { diagrams: [diagram] }

        const fileName = `${diagramData.name}.json`
        const fileText = JSON.stringify(diagrams, null, 2)
        this.saveFile(fileName, fileText)
    }

    writeDiagramObject(diagram) {
        const diagramData = diagram.DiagramData
        const diagramId = diagramData.diagramId

        this.writeDiagramData(diagramId, diagramData)
        Object.entries(diagram).forEach(property => {
            const key = property[0]
            const value = property[1]
            if (key === diagramDataKey) {
                return
            }
            this.writeData(this.canvasKey(diagramId, key), value)
        })
    }



    readDiagramObject(diagramId) {
        const diagramData = this.readDiagramData(diagramId)
        if (diagramData == null) {
            console.log('diagram not found', diagramId)
            return
        }

        const diagram = {}

        for (var i = 0, len = localStorage.length; i < len; i++) {
            var key = localStorage.key(i);
            if (key.startsWith(diagramKey)) {
                const parts = key.split('.')
                const id = parts[1]
                if (id === diagramId) {
                    const propertyName = parts[2]
                    const value = localStorage[key]
                    diagram[propertyName] = JSON.parse(value)
                }
            }
        }

        return diagram
    }


    saveFile(fileName, fileText) {
        const blob = new Blob([fileText], { type: "text/plain;charset=utf-8" });
        FileSaver.saveAs(blob, fileName);

    }

    loadFile(resultHandler) {
        const readFile = this.buildFileSelector(e => {
            var file = e.path[0].files[0];
            if (!file) {
                resultHandler(null);
            }

            const reader = new FileReader();
            reader.onload = e => resultHandler(e.target.result)
            reader.onerror = e => resultHandler(null)

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
        const diagramData = { diagramId: diagramId, systemId: systemId, name: name }
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
        if (diagramData == null) {
            return null
        }
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
        return `${diagramKey}.${diagramId}.${diagramDataKey}`
    }
}

export const store = new Store()

