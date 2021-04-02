import StoreFiles from "./StoreFiles"

const diagramKey = 'diagram'
const diagramDataKey = 'DiagramData'
const lastUsedDiagramKey = 'lastUsedDiagram'
const rootCanvasId = 'root'

class Store {
    files = new StoreFiles()

    newDiagram(diagramId, name, canvasData) {
        const diagramData = { diagramId: diagramId, name: name }
        this.writeDiagramData(diagramData)
        this.writeCanvas(canvasData)
        this.updateAccessedDiagram(diagramId)
    }

    setDiagramName(diagramId, name) {
        this.updateDiagramData(diagramId, { name: name })
    }

    getCanvas(diagramId, canvasId) {
        return this.readCanvas(diagramId, canvasId)
    }

    setCanvas(canvasData) {
        this.writeCanvas(canvasData)
        this.updateAccessedDiagram(canvasData.diagramId)
    }

    getRecentDiagramInfos() {
        const lastUsedDiagramId = this.readData(lastUsedDiagramKey)?.diagramId
        return this.readAllDiagramsInfos()
            .filter(d => d.id !== lastUsedDiagramId)
            .sort((i1, i2) => i1.accessed < i2.accessed ? -1 : i1.accessed > i2.accessed ? 1 : 0)
            .reverse()
    }

    getLastUsedCanvas() {
        const lastUsedDiagramId = this.readData(lastUsedDiagramKey)?.diagramId
        return this.getDiagramRootCanvas(lastUsedDiagramId)
    }

    getFirstDiagramRootCanvas() {
        const diagrams = this.getRecentDiagramInfos()
        const diagramId = diagrams[0]?.id
        return this.getDiagramRootCanvas(diagramId)
    }

    getDiagramRootCanvas(diagramId) {
        const diagramData = this.readDiagramData(diagramId)
        if (diagramData == null) {
            return null
        }

        this.updateAccessedDiagram(diagramId)
        return this.readCanvas(diagramId, rootCanvasId)
    }


    loadDiagramFromFile(resultHandler) {
        this.files.loadFile(file => {

            if (file == null) {
                console.warn('Failed load file')
                resultHandler(null)
                return
            }

            // Store all read diagrams
            file.diagrams.forEach(diagram => this.writeDiagram(diagram))

            let firstDiagramId = file.diagrams[0]?.diagramData.diagramId
            resultHandler(firstDiagramId)
        })
    }


    saveDiagramToFile(diagramId) {
        const diagram = this.readDiagram(diagramId)
        if (diagram == null) {
            return
        }

        const file = { diagrams: [diagram] }
        this.files.saveFile(`${diagram.diagramData.name}.json`, file)
    }

    saveAllDiagramsToFile() {
        const diagrams = this.readAllDiagramsInfos()
            .map(d => this.readDiagram(d.id))
            .filter(d => d != null)

        const file = { diagrams: diagrams }
        this.files.saveFile(`diagrams.json`, file)
    }


    // For printing 
    getDiagram(diagramId) {
        return this.readDiagram(diagramId)
    }

    deleteDiagram(diagramId) {
        this.removeDiagram(diagramId)
    }


    // private --------------------
    canvasKey(diagramId, canvasId) {
        return `${diagramKey}.${diagramId}.${canvasId}`
    }

    diagramKey(diagramId) {
        return `${diagramKey}.${diagramId}.${diagramDataKey}`
    }


    readCanvases(diagramId) {
        const keys = []

        for (var i = 0, len = localStorage.length; i < len; i++) {
            var key = localStorage.key(i);
            if (key.startsWith(diagramKey)) {
                const parts = key.split('.')
                const id = parts[1]
                const name = parts[2]
                if (id === diagramId && name !== diagramDataKey) {
                    keys.push(key)
                }
            }
        }

        return keys.map(key => this.readData(key)).filter(data => data != null)
    }

    removeDiagram(diagramId) {
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

        keys.forEach(key => this.removeData(key))
    }


    readAllDiagramsInfos() {
        const diagrams = []
        for (var i = 0, len = localStorage.length; i < len; i++) {
            var key = localStorage.key(i);
            if (key.endsWith(diagramDataKey)) {
                const value = JSON.parse(localStorage[key])
                diagrams.push({ id: value.diagramId, name: value.name, accessed: value.accessed })
            }
        }

        return diagrams
    }

    updateAccessedDiagram(diagramId) {
        this.writeData(lastUsedDiagramKey, { diagramId: diagramId })
        this.updateDiagramData(diagramId, { accessed: Date.now() })
    }


    writeDiagram(diagram) {
        this.writeDiagramData(diagram.diagramData)
        diagram.canvases.forEach(canvasData => this.writeCanvas(canvasData))
    }

    readDiagram(diagramId) {
        const diagramData = this.readDiagramData(diagramId)
        if (diagramData == null) {
            console.log('diagram not found', diagramId)
            return
        }
        const canvases = this.readCanvases(diagramId)
        const diagram = { diagramData: diagramData, canvases: canvases }

        return diagram
    }


    readDiagramData(diagramId) {
        return this.readData(this.diagramKey(diagramId))
    }

    writeDiagramData(diagramData) {
        this.writeData(this.diagramKey(diagramData.diagramId), diagramData)
    }

    updateDiagramData(diagramId, data) {
        const diagramData = this.readDiagramData(diagramId)
        if (diagramData == null) {
            return
        }
        this.writeDiagramData({ ...diagramData, ...data })
    }

    readCanvas(diagramId, canvasId) {
        return this.readData(this.canvasKey(diagramId, canvasId))
    }

    writeCanvas(canvasData) {
        const { diagramId, canvasId } = canvasData
        this.writeData(this.canvasKey(diagramId, canvasId), canvasData)
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

    removeData(key) {
        localStorage.removeItem(key)
    }
}

export const store = new Store()

